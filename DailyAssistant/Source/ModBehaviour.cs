using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[assembly: AssemblyTitle("时光餐厅日常助手")]
[assembly: AssemblyDescription("今日待答题辅助与原版结算打地鼠")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

namespace DailyAssistant
{
    public sealed class ModBehaviour : global::ModBehaviour
    {
        public static ModBehaviour Current { get; private set; }
        internal AssistantSettings Settings = new AssistantSettings();
        internal AssistantView View;
        internal bool AutoHamster;
        internal bool Busy;
        internal string LastResult = "尚未执行。奖励均由游戏原版结算。";
        internal string Status = "准备就绪";
        internal string BatchProgress = "";
        private Harmony harmony;
        private GameObject entry;
        private Texture2D entryTexture;
        private Sprite entryIcon;
        private Button entryButton;
        private MenuGroupUI mountedMenu;
        private string statePath;
        private string saveKey;
        private string rawIdentity;
        private float nextMount;
        private float nextHit;
        private readonly AppearanceDelay appearanceDelay = new AppearanceDelay();
        private readonly Dictionary<TMP_Text, Color> nativeHintColors = new Dictionary<TMP_Text, Color>();
        private bool originalBackground;
        private bool backgroundOwned;
        private bool stopped;
        private Coroutine job;
        private bool cancelJob;
        private bool hamsterBatch;
        private int batchHits;
        private long batchTeeth;
        private List<int> batchMasks = new List<int>();
        private int batchFull;
        private DailyRecord daily;
        private string jobSave;
        private string jobDate;
        private object jobData;
        private int jobInitialCount;
        private int jobTarget;
        private static readonly MethodInfo CanMaintain = AccessTools.Method(typeof(HamsterSceneController), "CanMaintainEntity");
        private static readonly MethodInfo Spawn = AccessTools.Method(typeof(HamsterSceneController), "TrySpawnCurrent");
        private static readonly MethodInfo DestroyLinger = AccessTools.Method(typeof(HamsterSceneController), "DestroySuccessLinger");
        private static readonly FieldInfo SpawnRoutine = AccessTools.Field(typeof(HamsterSceneController), "m_SpawnRoutine");
        private static readonly FieldInfo RestaurantButton = AccessTools.Field(typeof(MenuGroupUI), "m_btnRestaurant");

        protected override void OnAfterSetup()
        {
            if (Current != null && Current != this) throw new InvalidOperationException("日常助手已加载，请重启游戏后再更新。");
            if (CanMaintain == null || Spawn == null || SpawnRoutine == null || RestaurantButton == null || DestroyLinger == null
                || AccessTools.Field(typeof(QuizPanel), "m_todayQuizzes") == null
                || AccessTools.Field(typeof(QuizPanel), "m_nLocalIndex") == null
                || AccessTools.Field(typeof(QuizPanel), "m_optionTexts") == null)
                throw new InvalidOperationException("当前游戏接口与日常助手1.0不兼容，请更新模组。");
            Current = this;
            stopped = false;
            // 当前存档路径包含槽位名；统计另存于其父目录，切换槽位时仍按哈希键隔离。
            statePath = Path.Combine(Path.GetDirectoryName(GameWorld.GetCurrentSavePath()), "DailyAssistant", "state.json");
            LoadState();
            harmony = new Harmony("DailyAssistant.Local.1");
            harmony.PatchAll(typeof(ModBehaviour).Assembly);
            EventManager.Register("ENTER_MAIN_PANEL", OnLeave);
            EventManager.Register("GUEST_MODE_CHANGE", OnGuest);
            View = gameObject.AddComponent<AssistantView>();
            View.Owner = this;
            Log("1.1.1已加载；原版答题提示使用控件颜色；支持悬浮答案及2秒击打。");
        }

        protected override void OnBeforeDeactivate()
        {
            stopped = true;
            cancelJob = true;
            if (job != null) StopCoroutine(job);
            job = null;
            Busy = false;
            AutoHamster = false;
            SetBackground(false);
            if (harmony != null) harmony.UnpatchAll(harmony.Id);
            EventManager.Unregister("ENTER_MAIN_PANEL", OnLeave);
            EventManager.Unregister("GUEST_MODE_CHANGE", OnGuest);
            if (View != null) { View.Shutdown(); Destroy(View); }
            if (entry != null) Destroy(entry);
            if (entryIcon != null) Destroy(entryIcon);
            if (entryTexture != null) Destroy(entryTexture);
            SaveState();
            ClearNativeHints();
            if (Current == this) Current = null;
        }

        private void OnDestroy()
        {
            if (Current == this && !stopped) OnBeforeDeactivate();
        }

        private void Update()
        {
            if (stopped) return;
            try
            {
                if (Time.unscaledTime >= nextMount)
                {
                    nextMount = Time.unscaledTime + 1f;
                    MountEntry();
                    if (Ready()) EnsureContext();
                }
                if (entryButton != null) entryButton.interactable = !GameWorld.IsLoadingScene();
                var observedController = HamsterSceneController.Instance;
                var observed = observedController == null ? null : observedController.Current;
                appearanceDelay.Observe(observed == null ? null : (object)observed, Time.time);
                if (AutoHamster && !Busy && Ready() && Time.unscaledTime >= nextHit)
                {
                    nextHit = Time.unscaledTime + .08f;
                    var controller = HamsterSceneController.Instance;
                    if (CanPlay(controller)) HitCurrent(controller);
                }
                if (View != null) View.Tick();
            }
            catch (Exception ex)
            {
                AutoHamster = false;
                cancelJob = true;
                SetBackground(Busy);
                Status = "已暂停：" + CleanError(ex);
                LogError(ex);
                nextMount = Time.unscaledTime + 4f;
            }
        }

        internal bool Ready()
        {
            return !stopped && GameWorld.Instance != null && GameWorld.g_SQLiteSave != null
                && GameWorld.g_SQLiteSave.IsInitialized && !GameWorld.IsLoadingScene()
                && !GameWorld.IsExiting && !GameWorld.IsAppQuitting
                && !GuestSessionManager.IsInGuestSession()
                && mountedMenu != null && mountedMenu.gameObject.activeInHierarchy;
        }

        internal bool CanPlay(HamsterSceneController controller)
        {
            return Ready() && controller != null && (bool)CanMaintain.Invoke(controller, null);
        }

        private void MountEntry()
        {
            if (entry != null && mountedMenu != null) return;
            var menus = Resources.FindObjectsOfTypeAll<MenuGroupUI>();
            var menu = menus.FirstOrDefault(x => x.gameObject.scene.IsValid());
            if (menu == null) return;
            var original = RestaurantButton.GetValue(menu) as Button;
            if (original == null) return;
            var icon = LoadEntryIcon();
            entry = Instantiate(original.gameObject, original.transform.parent, false);
            entry.name = "DailyAssistant_Entry";
            entry.transform.SetSiblingIndex(original.transform.GetSiblingIndex());
            entryButton = entry.GetComponent<Button>();
            entryButton.onClick = new Button.ButtonClickedEvent();
            entryButton.onClick.AddListener(delegate { Open(); });
            var image = entry.GetComponent<Image>();
            var cursor = entry.GetComponent<UICursorHandler>();
            if (cursor != null) Destroy(cursor);
            if (icon != null && image != null)
            {
                image.sprite = icon;
                image.overrideSprite = null;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
            entry.AddComponent<AssistantHover>();
            var tip = entry.GetComponent<TooltipTrigger>();
            if (tip != null) tip.SetTextCallback(delegate(string a, string b) { return "日常助手\n今日答题 · 打地鼠"; }, "", false);
            mountedMenu = menu;
            LayoutRebuilder.MarkLayoutForRebuild(menu.transform as RectTransform);
            Log("已添加右上角入口。");
        }

        private Sprite LoadEntryIcon()
        {
            if (entryIcon != null) return entryIcon;
            string path = Path.Combine(Info.szPath, "icon.png");
            entryTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            entryTexture.name = "日常助手原创图标";
            if (!ImageConversion.LoadImage(entryTexture, File.ReadAllBytes(path), true))
            {
                Destroy(entryTexture);
                entryTexture = null;
                throw new InvalidDataException("日常助手图标读取失败，请重新安装完整模组包。");
            }
            entryTexture.filterMode = FilterMode.Bilinear;
            entryTexture.wrapMode = TextureWrapMode.Clamp;
            entryIcon = Sprite.Create(entryTexture, new Rect(0, 0, entryTexture.width, entryTexture.height), new Vector2(.5f, .5f), 100f);
            entryIcon.name = "厨师帽地鼠";
            return entryIcon;
        }

        internal void Open()
        {
            if (View == null) return;
            if (!Ready()) { MsgBox.Show("请先进入自己的餐厅。", 2f, null); return; }
            EnsureContext();
            View.Open();
        }

        internal void ToggleHints()
        {
            Settings.QuizHints = !Settings.QuizHints;
            if (!Settings.QuizHints && View != null) View.CloseFloatingAnswers();
            SaveState();
            RefreshNativeHints();
            View.Refresh();
        }

        internal void ToggleAuto()
        {
            if (Busy) return;
            AutoHamster = !AutoHamster;
            SetBackground(AutoHamster);
            Status = AutoHamster ? "自动打地鼠已开启，等待自然刷新" : "自动打地鼠已关闭";
            View.Refresh();
        }

        internal void SetBackground(bool enabled)
        {
            if (enabled && !backgroundOwned)
            {
                originalBackground = Application.runInBackground;
                Application.runInBackground = true;
                backgroundOwned = true;
            }
            else if (!enabled && backgroundOwned)
            {
                Application.runInBackground = originalBackground;
                backgroundOwned = false;
            }
        }

        private string ContextKey()
        {
            string identity = GameWorld.GetCurrentSavePath() + "|" + Player.m_szPlayerId + "|" + Player.m_nStartGameTime;
            if (identity != rawIdentity)
            {
                rawIdentity = identity;
                using (var sha = SHA256.Create())
                    saveKey = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
            }
            return saveKey;
        }

        internal DailyRecord EnsureContext()
        {
            if (!Ready()) return null;
            string key = ContextKey();
            string date = TimeHelper.GetTodayDateString();
            int completed = HamsterManager.GetSuccessIndex();
            daily = Settings.Records.FirstOrDefault(x => x.SaveKey == key && x.Date == date);
            if (daily == null)
            {
                daily = DailyRecord.Begin(key, date, completed);
                Settings.Records.Add(daily);
            }
            daily.Observe(completed);
            return daily;
        }

        internal void BeforeSettlement()
        {
            if (Ready()) EnsureContext();
        }

        internal void AfterSettlement(HamsterSettleResult result)
        {
            if (!result.bSuccess || !Ready() || daily == null) return;
            // 前置补丁已捕获结算前的进度，此处不能先用新进度建立统计。
            if (daily.SaveKey != ContextKey() || daily.Date != TimeHelper.GetTodayDateString()) return;
            if (!daily.Record(HamsterManager.GetSuccessIndex(), result.nBoardTeethGained,
                result.nUnlockedMaskId, result.bFullCollection)) return;
            if (hamsterBatch)
            {
                batchHits++;
                batchTeeth = checked(batchTeeth + result.nBoardTeethGained);
                if (result.nUnlockedMaskId >= 0) batchMasks.Add(result.nUnlockedMaskId);
                if (result.bFullCollection) batchFull++;
            }
            SaveState();
            if (View != null) View.MarkDirty();
        }

        internal string MaskName(int id)
        {
            var row = AvatarDressItemCatalog.Get((DressItemAsset.ItemType)3, ContentId.FromOfficial(id));
            return row == null ? "面具#" + id : row.szName;
        }

        internal string DailySummary()
        {
            var record = EnsureContext();
            if (record == null) return "进入自己的餐厅后可查看。";
            return "今日记录：" + record.RecordedCount + "只，板牙 +" + record.Teeth
                + (record.Masks.Count == 0 ? "，暂无新面具" : "\n面具：" + string.Join("、", record.Masks.Select(MaskName).ToArray()))
                + (record.FullCollectionRewards > 0 ? "\n含集齐补偿 " + record.FullCollectionRewards + "次（已计入板牙）" : "")
                + (record.HistoryIncomplete ? "\n启用前或读档前的部分收益未计入。" : "\n从今日首次成功前开始记录。")
                + "\n当前板牙余额：" + HamsterManager.GetBoardTeeth();
        }

        internal void RequestHamsterBatch()
        {
            if (Busy || !Ready()) return;
            var controller = HamsterSceneController.Instance;
            if (!CanPlay(controller)) { View.Notice("暂时无法打地鼠", "请在自己的餐厅营业，并退出装修、扩建或拍照状态。"); return; }
            int completed = HamsterManager.GetSuccessIndex();
            int remaining = Math.Max(0, HamsterManager.GetDailyQuota() - completed);
            if (remaining == 0) { View.Notice("今日已完成", "今天的地鼠次数已经全部完成，不会重复发放奖励。"); return; }
            string confirmSave = ContextKey();
            string confirmDate = TimeHelper.GetTodayDateString();
            object confirmData = HamsterManager.GetSaveData();
            View.Confirm("一键完成今日地鼠？", "已完成 " + completed + " 次，剩余 " + remaining
                + " 次。\n每只地鼠出现2秒后击打，连续完成剩余次数。奖励、板牙和面具由原版结算。\n请留在自己的餐厅，可随时停止。", delegate
            {
                if (!Ready() || confirmSave != ContextKey() || confirmDate != TimeHelper.GetTodayDateString()
                    || !ReferenceEquals(confirmData, HamsterManager.GetSaveData()))
                { View.Notice("状态已经变化", "请重新打开面板确认今日进度。"); return; }
                BeginJob(true);
            });
        }

        internal void RequestQuizBatch()
        {
            if (Busy || !Ready() || !Settings.QuizHints) return;
            var pending = QuizManager.GetTodayQuizList();
            if (pending.Count == 0) { View.Notice("今日已答完", "今天已经没有待答题目。"); return; }
            string confirmSave = ContextKey();
            string confirmDate = TimeHelper.GetTodayDateString();
            object confirmData = QuizManager.GetSaveData();
            View.Confirm("完成剩余答题？", "将为今日剩余的 " + pending.Count
                + " 道题提交正确答案，金币和经验由游戏原版发放。", delegate
            {
                if (!Ready() || confirmSave != ContextKey() || confirmDate != TimeHelper.GetTodayDateString()
                    || !ReferenceEquals(confirmData, QuizManager.GetSaveData()))
                { View.Notice("状态已经变化", "请重新读取待答题目。"); return; }
                BeginJob(false);
            });
        }

        private void BeginJob(bool hamster)
        {
            if (Busy || !Ready()) return;
            EnsureContext();
            Busy = true;
            cancelJob = false;
            hamsterBatch = hamster;
            jobSave = ContextKey();
            jobDate = TimeHelper.GetTodayDateString();
            jobData = hamster ? (object)HamsterManager.GetSaveData() : QuizManager.GetSaveData();
            jobInitialCount = hamster ? HamsterManager.GetSuccessIndex() : QuizManager.GetTodayAnsweredCount();
            jobTarget = hamster ? HamsterManager.GetDailyQuota() : QuizManager.GetTodayQuizCount();
            batchHits = 0; batchTeeth = 0; batchFull = 0; batchMasks.Clear();
            View.CloseAll();
            if (!hamster) UIManager.Close<QuizPanel>();
            SetBackground(true);
            job = StartCoroutine(RunJob(hamster));
        }

        private bool JobValid(bool hamster)
        {
            return !cancelJob && OperationGuard.IsCurrent(jobSave, jobDate, ContextKey(),
                TimeHelper.GetTodayDateString(), !GuestSessionManager.IsInGuestSession(), Ready())
                && ReferenceEquals(jobData, hamster ? (object)HamsterManager.GetSaveData() : QuizManager.GetSaveData());
        }

        private IEnumerator RunJob(bool hamster)
        {
            string failure = null;
            int answered = 0;
            int expected = jobInitialCount;
            long quizGold = 0, quizExp = 0;
            float lastProgress = Time.unscaledTime;
            try
            {
                while (true)
                {
                    bool done = false;
                    try
                    {
                        if (!JobValid(hamster)) { failure = "操作已停止：已取消、日期或餐厅状态发生变化。"; done = true; }
                        else if (hamster)
                        {
                            var controller = HamsterSceneController.Instance;
                            if (!CanPlay(controller)) { failure = "操作已停止：当前不满足原版地鼠出现条件。"; done = true; }
                            else if (HamsterManager.IsTodayComplete()) done = true;
                            else
                            {
                                int now = HamsterManager.GetSuccessIndex();
                                if (now != expected) { expected = now; lastProgress = Time.unscaledTime; }
                                BatchProgress = "地鼠处理中：今日 " + now + "/" + HamsterManager.GetDailyQuota()
                                    + " · 本次板牙 +" + batchTeeth + " · 点击入口可停止";
                                Status = "正在一键完成地鼠";
                                if (controller.Current == null)
                                {
                                    CancelScheduledSpawn(controller);
                                    if (DestroyLinger != null) DestroyLinger.Invoke(controller, null);
                                    Spawn.Invoke(controller, null);
                                }
                                HitCurrent(controller);
                                if (Time.unscaledTime - lastProgress > 12f)
                                { failure = "操作已停止：长时间未能成功击败地鼠，请检查餐厅是否有空位或被其他界面遮挡。"; done = true; }
                            }
                        }
                        else
                        {
                            var pending = QuizManager.GetTodayQuizList();
                            int index = QuizManager.GetTodayAnsweredCount();
                            if (pending.Count == 0) done = true;
                            else if (!OperationGuard.CanSubmit(expected, index, pending.Count))
                            { failure = "操作已停止：答题进度被其他操作改变，请重新读取。"; done = true; }
                            else if (pending[0] == null)
                            { failure = "操作已停止：题目不可用，可能来自已停用的模组。"; done = true; }
                            else
                            {
                                long gold = GameRule.nQuizRewardGold, exp = GameRule.nQuizRewardExp;
                                var question = pending[0];
                                if (!QuizManager.SubmitAnswer(question, question.nCorrectIndex))
                                { failure = "操作已停止：游戏未接受正确答案。"; done = true; }
                                else
                                {
                                    answered++; expected++;
                                    quizGold = checked(quizGold + gold); quizExp = checked(quizExp + exp);
                                    GameWorld.Active_SaveAll();
                                    BatchProgress = "答题处理中：本次已完成 " + answered + " 道";
                                    Status = "正在完成剩余答题";
                                }
                            }
                        }
                    }
                    catch (Exception ex) { failure = "操作已停止：" + CleanError(ex); LogError(ex); done = true; }
                    if (done) break;
                    yield return new WaitForSecondsRealtime(hamster ? .08f : .5f);
                }
            }
            finally
            {
                // 不重试已经进入原版结算的调用，防止异常后重复发奖。
                Busy = false;
                hamsterBatch = false;
                job = null;
                BatchProgress = "";
                SetBackground(AutoHamster);
                if (hamster)
                {
                    LastResult = (failure == null ? "地鼠一键完成。" : failure) + "\n本次成功 " + batchHits
                        + " 只，板牙 +" + batchTeeth
                        + (batchMasks.Count > 0 ? "\n获得面具：" + string.Join("、", batchMasks.Select(MaskName).ToArray()) : "\n本次未获得新面具。")
                        + (batchFull > 0 ? "\n面具集齐补偿 " + batchFull + " 次，已计入上述板牙。" : "");
                }
                else LastResult = (failure == null ? "剩余答题已完成。" : failure) + "\n本次答对 " + answered
                    + " 道，原版答题奖励：金币 +" + quizGold + "，经验 +" + quizExp + "。";
                Status = failure == null ? "本次操作已完成" : "本次操作已停止";
                SaveState();
                if (!stopped && Ready() && View != null) { View.Open(); View.Notice("本次结果", LastResult); }
                Log(Status + "；" + LastResult.Replace('\n', ' '));
            }
        }

        private void HitCurrent(HamsterSceneController controller)
        {
            var hamster = controller.Current;
            appearanceDelay.Observe(hamster == null ? null : (object)hamster, Time.time);
            if (hamster == null || !hamster.IsInteractive || !appearanceDelay.CanHit(hamster, Time.time)) return;
            // 满2秒后在同一帧完成所需点击，原版自行处理击败、奖励和消失。
            int hits = Math.Min(hamster.RemainingHits, 1024);
            Log("击打地鼠：出现后 " + appearanceDelay.ElapsedFor(hamster, Time.time).ToString("F3")
                + " 秒；所需点击 " + hamster.RemainingHits + " 次。");
            for (int i = 0; i < hits && hamster != null && controller.Current == hamster && hamster.IsInteractive; i++)
                hamster.Event_OnMouseClick();
        }

        internal void OnHamsterAppeared(HamsterObject hamster)
        {
            appearanceDelay.Reset(hamster, Time.time);
        }

        private static void CancelScheduledSpawn(HamsterSceneController controller)
        {
            var routine = SpawnRoutine.GetValue(controller) as Coroutine;
            if (routine != null) { controller.StopCoroutine(routine); SpawnRoutine.SetValue(controller, null); }
        }

        internal void Cancel() { cancelJob = true; Status = "正在停止，请等待当前处理结束"; }

        internal void OpenNativeQuiz()
        {
            if (!Ready() || Busy) return;
            var pending = QuizManager.GetTodayQuizList();
            if (pending.Count == 0) { View.Notice("今日已答完", "今天已经没有待答题目。"); return; }
            View.OpenFloatingAnswers();
            UIManager.Open<QuizPanel>(null).InitQuiz(pending, QuizManager.GetTodayAnsweredCount());
        }

        internal void ApplyNativeHints(QuizPanel panel)
        {
            if (panel == null) return;
            RestoreNativeHintColors(panel);
            if (!Settings.QuizHints) return;
            var list = AccessTools.Field(typeof(QuizPanel), "m_todayQuizzes").GetValue(panel) as List<QuizConstData>;
            int index = (int)AccessTools.Field(typeof(QuizPanel), "m_nLocalIndex").GetValue(panel);
            var texts = AccessTools.Field(typeof(QuizPanel), "m_optionTexts").GetValue(panel) as TMP_Text[];
            if (list == null || texts == null || index < 0 || index >= list.Count || list[index] == null) return;
            var options = list[index].GetOptions();
            for (int i = 0; i < texts.Length && i < options.Count; i++)
            {
                var text = texts[i];
                if (text == null) continue;
                if (!nativeHintColors.ContainsKey(text)) nativeHintColors.Add(text, text.color);
                bool correct = i == list[index].nCorrectIndex;
                // 原版选项关闭了富文本解析；直接设置颜色，不向文本中插入任何标签。
                string marker = text.font != null && text.font.HasCharacter('【') && text.font.HasCharacter('】')
                    ? "  【正确答案】" : "  (正确答案)";
                text.text = options[i] + (correct ? marker : "");
                text.color = correct ? (Color)new Color32(60, 129, 62, 255) : nativeHintColors[text];
            }
        }

        private void RestoreNativeHintColors(QuizPanel panel)
        {
            foreach (var pair in nativeHintColors)
                if (pair.Key != null && (panel == null || pair.Key.transform.IsChildOf(panel.transform)))
                    pair.Key.color = pair.Value;
            foreach (var dead in nativeHintColors.Keys.Where(x => x == null).ToArray()) nativeHintColors.Remove(dead);
        }

        private void RefreshNativeHints()
        {
            var panels = Resources.FindObjectsOfTypeAll<QuizPanel>();
            foreach (var panel in panels)
                if (panel.gameObject.scene.IsValid() && panel.gameObject.activeInHierarchy)
                {
                    if (Settings.QuizHints) ApplyNativeHints(panel);
                    else AccessTools.Method(typeof(QuizPanel), "ShowCurrentQuestion").Invoke(panel, null);
                }
        }

        private void ClearNativeHints()
        {
            RestoreNativeHintColors(null);
            bool old = Settings.QuizHints;
            Settings.QuizHints = false;
            try { RefreshNativeHints(); } catch { }
            Settings.QuizHints = old;
            nativeHintColors.Clear();
        }

        private void OnLeave() { cancelJob = true; appearanceDelay.Reset(null, Time.time); if (View != null) View.CloseAll(); }
        private void OnGuest(object value) { cancelJob = true; appearanceDelay.Reset(null, Time.time); if (View != null) View.CloseAll(); }

        private void LoadState()
        {
            try
            {
                if (File.Exists(statePath)) Settings = JsonConvert.DeserializeObject<AssistantSettings>(File.ReadAllText(statePath, Encoding.UTF8)) ?? new AssistantSettings();
                if (Settings.Records == null) Settings.Records = new List<DailyRecord>();
                foreach (var record in Settings.Records) if (record.Masks == null) record.Masks = new List<int>();
            }
            catch (Exception ex) { Settings = new AssistantSettings(); LogError(ex); }
        }

        private void SaveState()
        {
            try
            {
                if (string.IsNullOrEmpty(statePath)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(statePath));
                // 限制统计文件体积，最多保留最近180条存档日记录。
                if (Settings.Records.Count > 180) Settings.Records = Settings.Records.Skip(Settings.Records.Count - 180).ToList();
                string temp = statePath + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(Settings, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(statePath)) File.Replace(temp, statePath, statePath + ".bak");
                else File.Move(temp, statePath);
            }
            catch (Exception ex) { LogError(ex); Status = "功能可用，但统计文件保存失败"; }
        }

        internal static string CleanError(Exception ex)
        {
            var wrapped = ex as TargetInvocationException;
            return wrapped != null && wrapped.InnerException != null ? wrapped.InnerException.Message : ex.Message;
        }
        internal static void Log(string text) { Debug.Log("[日常助手] " + text); }
        internal static void LogError(Exception ex) { Debug.LogError("[日常助手] " + ex); }
    }

    [HarmonyPatch(typeof(HamsterObject), "Initialize")]
    internal static class AppearancePatch
    {
        private static void Postfix(HamsterObject __instance, bool __result)
        {
            try { if (__result && ModBehaviour.Current != null) ModBehaviour.Current.OnHamsterAppeared(__instance); }
            catch (Exception ex) { ModBehaviour.LogError(ex); }
        }
    }

    [HarmonyPatch(typeof(QuizPanel), "ShowCurrentQuestion")]
    internal static class QuizHintPatch
    {
        private static void Postfix(QuizPanel __instance)
        {
            try { if (ModBehaviour.Current != null) ModBehaviour.Current.ApplyNativeHints(__instance); }
            catch (Exception ex) { ModBehaviour.LogError(ex); }
        }
    }

    [HarmonyPatch(typeof(HamsterManager), "SettleSuccess")]
    internal static class SettlementPatch
    {
        private static void Prefix()
        {
            try { if (ModBehaviour.Current != null) ModBehaviour.Current.BeforeSettlement(); }
            catch (Exception ex) { ModBehaviour.LogError(ex); }
        }
        private static void Postfix(HamsterSettleResult __result)
        {
            try { if (ModBehaviour.Current != null) ModBehaviour.Current.AfterSettlement(__result); }
            catch (Exception ex) { ModBehaviour.LogError(ex); }
        }
    }
}
