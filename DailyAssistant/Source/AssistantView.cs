using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DailyAssistant
{
    public sealed class AssistantHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Vector3 original;
        private bool hovering;
        private void Awake() { original = transform.localScale; }
        public void OnPointerEnter(PointerEventData e) { hovering = true; }
        public void OnPointerExit(PointerEventData e) { hovering = false; }
        public void OnPointerClick(PointerEventData e) { }
        private void Update() { transform.localScale = Vector3.Lerp(transform.localScale, original * (hovering ? 1.06f : 1f), Time.unscaledDeltaTime * 18f); }
        private void OnDisable() { hovering = false; transform.localScale = original; }
    }

    // 只在标题栏拖动；正文的拖动仍留给滚动区。
    public sealed class AnswerWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        internal RectTransform Target;
        internal RectTransform CanvasRect;
        private Vector2 pointerStart;
        private Vector2 positionStart;
        public void OnBeginDrag(PointerEventData e)
        {
            if (Target == null) return;
            pointerStart = e.position;
            positionStart = Target.anchoredPosition;
        }
        public void OnDrag(PointerEventData e)
        {
            if (Target == null || CanvasRect == null) return;
            var canvas = CanvasRect.GetComponent<Canvas>();
            float scale = canvas == null ? 1f : Math.Max(.01f, canvas.scaleFactor);
            Vector2 point = positionStart + (e.position - pointerStart) / scale;
            point.x = Mathf.Clamp(point.x, Math.Min(-12, -CanvasRect.rect.width + Target.rect.width + 12), -12);
            point.y = Mathf.Clamp(point.y, Math.Min(-12, -CanvasRect.rect.height + Target.rect.height + 12), -12);
            Target.anchoredPosition = point;
        }
    }

    public sealed class AssistantView : MonoBehaviour, IEscHandler
    {
        internal ModBehaviour Owner;
        private GameObject canvasObject;
        private RectTransform root;
        private RectTransform panel;
        private RectTransform body;
        private GameObject modal;
        private TMP_Text status;
        private TMP_Text progress;
        private TMP_FontAsset font;
        private Material fontMaterial;
        private Sprite panelSprite;
        private Sprite confirmSprite;
        private Sprite cancelSprite;
        private Sprite cardSprite;
        private Color ink = new Color32(89, 61, 37, 255);
        private bool visible;
        private bool registered;
        private bool dirty;
        private int tab;
        private float nextRefresh;
        private string signature;
        private bool destroyed;
        private Action modalConfirm;
        private GameObject content;
        private RectTransform floatingAnswers;
        private RectTransform floatingRows;
        private TMP_Text floatingCount;
        private string floatingSignature;
        private string floatingDate;
        private object floatingSaveData;
        private float nextFloatingRefresh;

        internal void Open()
        {
            if (destroyed) return;
            if (canvasObject == null) Build();
            visible = true;
            canvasObject.SetActive(true);
            root.gameObject.SetActive(true);
            UpdateEscRegistration();
            Refresh();
        }

        internal void Close()
        {
            visible = false;
            CloseModal();
            if (root != null) root.gameObject.SetActive(false);
            UpdateEscRegistration();
            UpdateCanvasVisibility();
        }

        public bool OnEscapePressed()
        {
            if (modal != null) { CloseModal(); return true; }
            if (visible) { Close(); return true; }
            if (floatingAnswers != null) { CloseFloatingAnswers(); return true; }
            return false;
        }

        internal void Shutdown()
        {
            destroyed = true;
            CloseAll();
            if (canvasObject != null) Destroy(canvasObject);
        }

        internal void MarkDirty() { dirty = true; }

        internal void CloseAll()
        {
            CloseFloatingAnswers();
            Close();
        }

        private void UpdateEscRegistration()
        {
            bool needed = visible || floatingAnswers != null;
            if (needed && !registered) { UIManager.RegisterEscHandler(this); registered = true; }
            else if (!needed && registered) { UIManager.UnregisterEscHandler(this); registered = false; }
        }

        private void UpdateCanvasVisibility()
        {
            if (canvasObject != null) canvasObject.SetActive(visible || floatingAnswers != null || (Owner != null && Owner.Busy));
        }

        internal void Tick()
        {
            if (destroyed || Owner == null || canvasObject == null) return;
            if (Owner.Busy)
            {
                canvasObject.SetActive(true);
                progress.gameObject.SetActive(true);
                progress.text = Owner.BatchProgress;
            }
            else progress.gameObject.SetActive(false);
            if (floatingAnswers != null)
            {
                if (!Owner.Ready() || !Owner.Settings.QuizHints || floatingDate != TimeHelper.GetTodayDateString()
                    || !ReferenceEquals(floatingSaveData, QuizManager.GetSaveData())) CloseFloatingAnswers();
                else if (Time.unscaledTime >= nextFloatingRefresh)
                {
                    nextFloatingRefresh = Time.unscaledTime + .5f;
                    RefreshFloatingAnswers(false);
                }
            }
            if (!visible) { UpdateCanvasVisibility(); return; }
            if (!Owner.Ready()) { CloseAll(); return; }
            status.text = TimeHelper.GetNowTime().ToString("yyyy年M月d日") + "  ·  " + Owner.Status;
            if (Time.unscaledTime < nextRefresh && !dirty) return;
            nextRefresh = Time.unscaledTime + 1f;
            string now = StateSignature();
            if (dirty || signature != now) Refresh();
        }

        private string StateSignature()
        {
            return tab + "|" + TimeHelper.GetTodayDateString() + "|" + Owner.Settings.QuizHints + "|"
                + Owner.AutoHamster + "|" + Owner.Busy + "|" + QuizManager.GetTodayAnsweredCount()
                + "|" + HamsterManager.GetSuccessIndex() + "|" + HamsterManager.GetBoardTeeth();
        }

        private void FindTheme()
        {
            var text = Resources.FindObjectsOfTypeAll<TMP_Text>().FirstOrDefault(x => x.font != null && x.gameObject.scene.IsValid());
            if (text != null) { font = text.font; fontMaterial = text.fontSharedMaterial; }
            else font = TMP_Settings.defaultFontAsset;
            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            panelSprite = sprites.FirstOrDefault(x => x.name == "Tooltip_Bg");
            cardSprite = sprites.FirstOrDefault(x => x.name == "time_tips_bg");
            confirmSprite = sprites.FirstOrDefault(x => x.name == "Btn_Confirm");
            cancelSprite = sprites.FirstOrDefault(x => x.name == "Btn_Cancel");
        }

        private void Build()
        {
            FindTheme();
            canvasObject = new GameObject("日常助手界面", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            root = Rect("主界面", canvasObject.transform);
            Stretch(root);
            var shade = Image(root, null, new Color(0, 0, 0, .46f));
            var shadeButton = root.gameObject.AddComponent<Button>();
            shadeButton.targetGraphic = shade;
            shadeButton.transition = Selectable.Transition.None;
            shadeButton.onClick.AddListener(delegate { if (modal == null) Close(); });

            panel = Box("手账面板", root, 0, 0, 980, 800, panelSprite, new Color32(255, 248, 224, 255));
            // 面板接收点击，避免点内容时穿透到遮罩。
            var panelBlock = panel.gameObject.AddComponent<Button>();
            panelBlock.transition = Selectable.Transition.None;
            panelBlock.targetGraphic = panel.GetComponent<Image>();
            Label(panel, "日常助手", 34, 30, 700, 54, 34, ink, TextAlignmentOptions.Left);
            Label(panel, "1.1.0  ·  今日答题与地鼠小帮手", 36, 86, 740, 32, 19, new Color32(146, 111, 74, 255), TextAlignmentOptions.Left);
            ActionButton(panel, "关闭", 830, 36, 114, 48, false, Close);
            status = Label(panel, "", 36, 126, 900, 32, 18, ink, TextAlignmentOptions.Left);
            ActionButton(panel, "今日答题", 36, 170, 194, 52, true, delegate { tab = 0; Refresh(); });
            ActionButton(panel, "打地鼠", 242, 170, 194, 52, true, delegate { tab = 1; Refresh(); });
            Label(panel, "原版奖励 · 不重置每日次数", 520, 180, 414, 34, 18, new Color32(135, 106, 75, 255), TextAlignmentOptions.Right);
            body = Rect("功能内容", panel);
            Place(body, 34, 236, 912, 526);
            progress = Label(canvasObject.transform, "", 0, 0, 1100, 48, 23, Color.white, TextAlignmentOptions.Center);
            progress.rectTransform.anchorMin = new Vector2(.5f, 1);
            progress.rectTransform.anchorMax = new Vector2(.5f, 1);
            progress.rectTransform.pivot = new Vector2(.5f, 1);
            progress.rectTransform.anchoredPosition = new Vector2(0, -108);
            progress.gameObject.SetActive(false);
        }

        internal void Refresh()
        {
            if (!visible || body == null || Owner == null || !Owner.Ready()) return;
            dirty = false;
            signature = StateSignature();
            if (content != null) { content.SetActive(false); Destroy(content); }
            content = new GameObject("页面", typeof(RectTransform));
            content.transform.SetParent(body, false);
            Stretch((RectTransform)content.transform);
            if (Owner.Busy) BuildBusy(content.transform);
            else if (tab == 0) BuildQuiz(content.transform);
            else BuildHamster(content.transform);
        }

        private void BuildBusy(Transform parent)
        {
            Label(parent, "正在处理，请稍候", 24, 25, 850, 50, 30, ink, TextAlignmentOptions.Center);
            Label(parent, Owner.BatchProgress, 24, 100, 850, 100, 24, ink, TextAlignmentOptions.Center);
            Label(parent, "停止后保留原版已经结算的进度和奖励。", 24, 220, 850, 60, 22, ink, TextAlignmentOptions.Center);
            ActionButton(parent, "停止本次操作", 294, 320, 300, 62, false, delegate { Owner.Cancel(); Close(); });
        }

        private void BuildQuiz(Transform parent)
        {
            bool enabled = Owner.Settings.QuizHints;
            ActionButton(parent, enabled ? "答题辅助：已开启" : "答题辅助：已关闭", 6, 0, 270, 50, enabled, Owner.ToggleHints);
            var pending = QuizManager.GetTodayQuizList();
            Label(parent, "今日待答 " + pending.Count + " 道  ·  已答 " + QuizManager.GetTodayAnsweredCount() + " 道", 300, 7, 590, 38, 21, ink, TextAlignmentOptions.Right);
            if (!enabled)
            {
                Label(parent, "开启答题辅助后，可查看今日待答题目与正确答案。\n\n你可以自己选择答案，也可以让助手完成剩余答题。", 56, 110, 790, 170, 26, ink, TextAlignmentOptions.Center);
                return;
            }
            if (pending.Count == 0)
            {
                Label(parent, "今日答题已完成", 60, 140, 780, 70, 32, ink, TextAlignmentOptions.Center);
                Label(parent, "没有待答题目。明日刷新后再来看看。", 60, 220, 780, 60, 23, ink, TextAlignmentOptions.Center);
                ActionButton(parent, "打开悬浮答案", 292, 350, 320, 58, false, OpenFloatingAnswers);
                return;
            }
            var scroll = Scroll(parent, 4, 65, 900, 354);
            float y = 0;
            int baseIndex = QuizManager.GetTodayAnsweredCount();
            for (int i = 0; i < pending.Count; i++)
            {
                var q = pending[i];
                string question = q == null ? "题目不可用，可能来自已停用的模组。" : q.szQuestion;
                string answer = "";
                if (q != null)
                {
                    var options = q.GetOptions();
                    answer = q.nCorrectIndex >= 0 && q.nCorrectIndex < options.Count ? options[q.nCorrectIndex] : "答案不可用";
                }
                string title = "第 " + (baseIndex + i + 1) + " 题  ·  " + Escape(question);
                string line = "正确答案：" + Escape(answer);
                int lines = Math.Max(1, (question.Length + 30) / 31);
                float height = Math.Max(118, 42 + lines * 30 + 45);
                var card = Box("待答题目", scroll, 0, y, 876, height - 10, cardSprite, new Color32(244, 231, 199, 255));
                Label(card, title, 22, 14, 830, height - 68, 23, ink, TextAlignmentOptions.TopLeft);
                Label(card, line, 22, height - 58, 830, 38, 23, new Color32(60, 116, 52, 255), TextAlignmentOptions.Left);
                y += height;
            }
            scroll.sizeDelta = new Vector2(0, y);
            ActionButton(parent, "悬浮答案", 8, 442, 282, 58, false, OpenFloatingAnswers);
            ActionButton(parent, "自己答题", 306, 442, 282, 58, false, Owner.OpenNativeQuiz);
            var auto = ActionButton(parent, "完成剩余答题", 604, 442, 282, 58, true, Owner.RequestQuizBatch);
            auto.interactable = pending.All(x => x != null);
        }

        private void BuildHamster(Transform parent)
        {
            int completed = HamsterManager.GetSuccessIndex();
            int quota = HamsterManager.GetDailyQuota();
            Label(parent, "今日进度  " + completed + " / " + quota, 12, 0, 500, 46, 28, ink, TextAlignmentOptions.Left);
            ActionButton(parent, "查看上次操作结果", 590, 0, 306, 44, false, delegate { Notice("上次操作结果", Owner.LastResult); });
            var first = Box("一键功能", parent, 4, 62, 902, 116, cardSprite, new Color32(244, 231, 199, 255));
            Label(first, "一键完成", 20, 12, 510, 35, 25, ink, TextAlignmentOptions.Left);
            Label(first, "二次确认后连续完成，每只出现2秒后击打。", 20, 54, 562, 48, 19, ink, TextAlignmentOptions.TopLeft);
            var batch = ActionButton(first, completed >= quota ? "今日已完成" : "完成剩余 " + (quota - completed) + " 次", 600, 28, 280, 58, true, Owner.RequestHamsterBatch);
            batch.interactable = completed < quota;
            var second = Box("自动功能", parent, 4, 190, 902, 120, cardSprite, new Color32(244, 231, 199, 255));
            Label(second, "自动打地鼠", 20, 12, 510, 35, 25, ink, TextAlignmentOptions.Left);
            Label(second, "自然刷出后等待2秒再击打，请留在自己的餐厅营业。\n游戏运行时支持后台，关闭游戏或休眠后不工作。", 20, 51, 576, 60, 18, ink, TextAlignmentOptions.TopLeft);
            ActionButton(second, Owner.AutoHamster ? "关闭自动击打" : "开启自动击打", 600, 30, 280, 58, !Owner.AutoHamster, Owner.ToggleAuto);
            var summary = Box("收益记录", parent, 4, 322, 902, 184, cardSprite, new Color32(250, 240, 216, 255));
            Label(summary, Owner.DailySummary(), 22, 12, 850, 154, 21, ink, TextAlignmentOptions.TopLeft);
        }

        internal void OpenFloatingAnswers()
        {
            if (destroyed || Owner == null || !Owner.Ready() || !Owner.Settings.QuizHints) return;
            if (canvasObject == null) Build();
            if (floatingAnswers == null)
            {
                floatingAnswers = Box("独立悬浮答案", canvasObject.transform, 0, 0, 450, 560,
                    panelSprite, new Color32(255, 248, 224, 255));
                floatingAnswers.anchorMin = floatingAnswers.anchorMax = floatingAnswers.pivot = new Vector2(1, 1);
                floatingAnswers.anchoredPosition = new Vector2(-26, -170);
                var header = Rect("可拖动标题栏", floatingAnswers);
                Place(header, 14, 10, 332, 44);
                Image(header, null, new Color(1, 1, 1, .01f));
                Label(header, "今日待答 · 悬浮答案", 6, 0, 322, 44, 23, ink, TextAlignmentOptions.Left);
                var drag = header.gameObject.AddComponent<AnswerWindowDrag>();
                drag.Target = floatingAnswers;
                drag.CanvasRect = canvasObject.transform as RectTransform;
                ActionButton(floatingAnswers, "关闭", 354, 14, 78, 40, false, CloseFloatingAnswers);
                floatingCount = Label(floatingAnswers, "", 20, 62, 410, 30, 18, ink, TextAlignmentOptions.Left);
                floatingRows = Scroll(floatingAnswers, 14, 102, 422, 410);
                Label(floatingAnswers, "拖动标题栏可移动 · 答题后自动更新", 20, 524, 410, 26, 16,
                    new Color32(146, 111, 74, 255), TextAlignmentOptions.Left);
                floatingDate = TimeHelper.GetTodayDateString();
                floatingSaveData = QuizManager.GetSaveData();
            }
            floatingAnswers.SetAsLastSibling();
            RefreshFloatingAnswers(true);
            Close();
            UpdateEscRegistration();
            UpdateCanvasVisibility();
            ModBehaviour.Log("已打开独立悬浮答案，主面板关闭后保持显示。");
        }

        internal void CloseFloatingAnswers()
        {
            if (floatingAnswers != null)
            {
                floatingAnswers.gameObject.SetActive(false);
                Destroy(floatingAnswers.gameObject);
            }
            floatingAnswers = null;
            floatingRows = null;
            floatingCount = null;
            floatingSaveData = null;
            floatingSignature = null;
            UpdateEscRegistration();
            UpdateCanvasVisibility();
        }

        private void RefreshFloatingAnswers(bool force)
        {
            if (floatingAnswers == null || floatingRows == null) return;
            var pending = QuizManager.GetTodayQuizList();
            int answered = QuizManager.GetTodayAnsweredCount();
            string current = TimeHelper.GetTodayDateString() + "|" + answered + "|"
                + string.Join("|", pending.Select(q => q == null ? "未知" : q.nQuizID.ToString() + q.szQuestion + q.nCorrectIndex).ToArray());
            if (!force && floatingSignature == current) return;
            floatingSignature = current;
            floatingCount.text = "尚有 " + pending.Count + " 道待答题，已答 " + answered + " 道";
            for (int i = floatingRows.childCount - 1; i >= 0; i--)
            {
                var old = floatingRows.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            if (pending.Count == 0)
            {
                Label(floatingRows, "今日答题已完成。\n没有待答题目。", 18, 30, 374, 110, 23, ink, TextAlignmentOptions.TopLeft);
                floatingRows.sizeDelta = new Vector2(0, 140);
                floatingRows.anchoredPosition = Vector2.zero;
                return;
            }
            float y = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                var q = pending[i];
                string question = q == null ? "题目不可用" : q.szQuestion;
                string answer = "答案不可用";
                if (q != null)
                {
                    var options = q.GetOptions();
                    if (q.nCorrectIndex >= 0 && q.nCorrectIndex < options.Count)
                        answer = ((char)('A' + q.nCorrectIndex)) + ". " + options[q.nCorrectIndex];
                }
                int questionLines = Math.Max(1, (question.Length + 18) / 19);
                int answerLines = Math.Max(1, (answer.Length + 14) / 15);
                float questionHeight = 28 + questionLines * 25;
                float answerHeight = 12 + answerLines * 25;
                float height = questionHeight + answerHeight + 28;
                var card = Box("待答题及答案", floatingRows, 0, y, 410, height - 8, cardSprite,
                    new Color32(244, 231, 199, 255));
                Label(card, "第 " + (answered + i + 1) + " 题  " + Escape(question), 14, 10, 380, questionHeight,
                    20, ink, TextAlignmentOptions.TopLeft);
                Label(card, "正确答案：" + Escape(answer), 14, questionHeight + 8, 380, answerHeight,
                    21, new Color32(60, 116, 52, 255), TextAlignmentOptions.TopLeft);
                y += height;
            }
            floatingRows.sizeDelta = new Vector2(0, y);
            floatingRows.anchoredPosition = Vector2.zero;
        }

        internal void Confirm(string title, string message, Action confirm)
        {
            ShowModal(title, message, confirm);
        }

        internal void Notice(string title, string message)
        {
            if (!visible) Open();
            ShowModal(title, message, null);
        }

        private void ShowModal(string title, string message, Action confirm)
        {
            CloseModal();
            modalConfirm = confirm;
            var shade = Rect("二次确认", root);
            Stretch(shade);
            Image(shade, null, new Color(0, 0, 0, .42f));
            modal = shade.gameObject;
            var card = Box("确认内容", shade, 0, 0, 760, 430, panelSprite, new Color32(255, 248, 224, 255));
            Label(card, title, 34, 30, 690, 50, 30, ink, TextAlignmentOptions.Center);
            Label(card, Escape(message), 42, 104, 676, 200, 24, ink, TextAlignmentOptions.TopLeft);
            if (confirm == null)
                ActionButton(card, "知道了", 250, 336, 260, 58, true, CloseModal);
            else
            {
                ActionButton(card, "取消", 74, 336, 260, 58, false, CloseModal);
                ActionButton(card, "确认执行", 426, 336, 260, 58, true, delegate
                {
                    var action = modalConfirm;
                    CloseModal();
                    if (action != null) action();
                });
            }
        }

        private void CloseModal()
        {
            modalConfirm = null;
            if (modal != null) { modal.SetActive(false); Destroy(modal); modal = null; }
        }

        private RectTransform Scroll(Transform parent, float x, float y, float w, float h)
        {
            var frame = Rect("题目滚动区", parent); Place(frame, x, y, w, h);
            var view = Rect("可视范围", frame); Stretch(view);
            view.gameObject.AddComponent<RectMask2D>();
            Image(view, null, new Color(1, 1, 1, .005f));
            var list = Rect("题目列表", view);
            list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1); list.pivot = new Vector2(0, 1);
            list.anchoredPosition = Vector2.zero;
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = list; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
            return list;
        }

        private RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Sprite sprite, Color color)
        {
            var rect = Rect(name, parent);
            if (parent == root || (modal != null && parent == modal.transform))
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
            }
            else Place(rect, x, y, w, h);
            Image(rect, sprite, sprite == null ? color : Color.white);
            return rect;
        }

        private TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions alignment)
        {
            var rect = Rect("文字", parent); Place(rect, x, y, w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            if (fontMaterial != null) label.fontSharedMaterial = fontMaterial;
            label.text = text; label.fontSize = size; label.color = color; label.alignment = alignment;
            label.raycastTarget = false; label.richText = true;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        private Button ActionButton(Transform parent, string caption, float x, float y, float w, float h, bool positive, Action action)
        {
            var rect = Rect(caption, parent); Place(rect, x, y, w, h);
            var image = Image(rect, positive ? confirmSprite : cancelSprite,
                positive ? new Color32(155, 196, 81, 255) : new Color32(249, 205, 72, 255));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
            colors.pressedColor = new Color(.87f, .87f, .87f, 1); colors.disabledColor = new Color(.7f, .7f, .7f, .6f); button.colors = colors;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            if (action != null) button.onClick.AddListener(delegate { action(); });
            var label = Label(rect, caption, 10, 0, w - 20, h, Math.Min(23, h * .42f), ink, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            return button;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
        private static Image Image(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = color;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0 ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            return image;
        }
        private static string Escape(string text) { return (text ?? "").Replace("<", "＜").Replace(">", "＞"); }
    }
}
