using System;
using System.IO;
using System.Text;
using AdvancedInputFieldPlugin;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Preview scenes do not run ordinary MonoBehaviour lifecycle callbacks.
    // Register the disposable EventSystem explicitly for standard UI selection.
    public sealed class KeyboardInspectionEventSystem : EventSystem
    {
        public void ActivateForInspection() => base.OnEnable();
        public void ReleaseForInspection() => base.OnDisable();
    }

    // Disposable prefab fixtures only: no account, save, withdrawal or network flow.
    public static class WithdrawalKeyboardInspection
    {
        const string Form = "Assets/BizzaWZ/Final/Real/UI/WithdrawFillPanel/WithdrawFillPanel.prefab";
        const string Service = "Assets/BizzaWZ/Final/Real/UI/ServicePanel/ServicePanel.prefab";
        static readonly string Output = Path.GetFullPath("../Validation/WithdrawalKeyboard-20261008");

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var report = new StringBuilder();
            var previousEventSystem = EventSystem.current;
            try
            {
                foreach (int height in new[] { 1920, 2400 })
                    InspectForm(height, report);
                InspectService(report);
                report.AppendLine("PASS: saved prefab geometry, native keyboard callback, drag bounds, focus changes and keyboard close.");
                report.AppendLine("No account data read/written; physical Android keyboard not exercised.");
                File.WriteAllText(Path.Combine(Output, "inspection.txt"), report.ToString());
            }
            catch (Exception e)
            {
                report.AppendLine("FAIL: " + e);
                File.WriteAllText(Path.Combine(Output, "inspection.txt"), report.ToString());
                throw;
            }
            finally { EventSystem.current = previousEventSystem; }
        }

        static void InspectForm(int height, StringBuilder report)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            KeyboardInspectionEventSystem events = null;
            try
            {
                var canvas = MakeCanvas(scene, height);
                events = new GameObject("KeyboardInspectionEvents", typeof(KeyboardInspectionEventSystem)).GetComponent<KeyboardInspectionEventSystem>();
                SceneManager.MoveGameObjectToScene(events.gameObject, scene);
                events.ActivateForInspection();
                EventSystem.current = events;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Form), scene);
                go.transform.SetParent(canvas.transform, false);
                var pageRoot = (RectTransform)go.transform;
                var content = (RectTransform)go.transform.Find("Root");
                var keyboard = go.GetComponent<KeyBoardPanel>();
                var data = new SerializedObject(keyboard);
                Require(data.FindProperty("panel").objectReferenceValue == content, "Keyboard must move authored Root content, not page root.");
                data.FindProperty("transitionTime").floatValue = 0f;
                data.ApplyModifiedPropertiesWithoutUndo();
                var presentation = go.GetComponent<WithdrawalFormPresentation>();
                var page = go.GetComponent<UIWithdrawalPanel>();
                int cases = 0;
                foreach (var preset in presentation.presets)
                {
                    events.SetSelectedGameObject(null);
                    keyboard.OnKeyboardHeightChanged(0);
                    preset.Apply();
                    // Match the selected payment method's serialized visibility lists
                    // without opening an account flow or loading saved identity data.
                    foreach (var list in new[] { page.PIXObjList, page.PagBankList, page.PaypalList, page.OVOList, page.ErrorList })
                        foreach (var item in list) if (item != null) item.SetActive(false);
                    var visible = preset.name == "PIX" ? page.PIXObjList : preset.name == "PagBank" ? page.PagBankList :
                        preset.name == "DANA" || preset.name == "OVO" ? page.OVOList : page.PaypalList;
                    foreach (var item in visible) if (item != null) item.SetActive(true);
                    page.PlatformRoot.SetActive(false);
                    page.PlatformIconRoot.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(page.InputRoot);
                    if (!preset.fixedLayout)
                        page.BG.sizeDelta = new Vector2(page.BG.sizeDelta.x, page.defaultHeight + page.InputRoot.rect.height);
                    Canvas.ForceUpdateCanvases();
                    var inputs = preset.name == "PIX" ? new[] { page.CPFNumberInput, page.accountNameInput, page.accountIdentificationInput } :
                        preset.name == "PagBank" ? new[] { page.CPFNumberInput, page.accountNameInput, page.paypalMailInput } :
                        preset.name == "DANA" || preset.name == "OVO" ? new[] { page.accountNameInput, page.accPhoneMailInput } :
                        new[] { page.paypalMailInput };
                    var origin = content.anchoredPosition;
                    var pageOrigin = pageRoot.anchoredPosition;
                    var underlay = (RectTransform)go.transform.Find("OpaqueUnderlay");
                    var underlayCorners = Corners(underlay);
                    var backdrop = (RectTransform)go.transform.Find("ReferenceBackdrop");
                    var backdropCorners = Corners(backdrop);
                    var pixBackdrop = (RectTransform)go.transform.Find("PixArtwork/Backdrop");
                    var pixCorners = Corners(pixBackdrop);
                    var danaBackdrop = (RectTransform)go.transform.Find("DanaArtwork/Backdrop");
                    var danaCorners = Corners(danaBackdrop);
                    foreach (int keyboardHeight in new[] { 700, 1000 })
                    {
                        foreach (var input in inputs)
                        {
                            if (!input.transform.IsChildOf(content)) continue;
                            events.SetSelectedGameObject(input.gameObject);
                            keyboard.OnKeyboardHeightChanged(keyboardHeight);
                            Require(keyboard.GetInputToKeyboardHeight() >= 0f,
                                preset.name + ": focused field is hidden under keyboard: " + input.name +
                                " active=" + input.gameObject.activeInHierarchy + " gap=" + keyboard.GetInputToKeyboardHeight() +
                                " selected=" + (EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null) +
                                " offset=" + content.anchoredPosition + " bounds=" + keyboard.panelSizeRect.rect);
                            var firstPosition = content.anchoredPosition;
                            keyboard.OnKeyboardHeightChanged(keyboardHeight);
                            Require(Vector2.Distance(firstPosition, content.anchoredPosition) < .1f,
                                "Repeated keyboard notification must not accumulate offset: " + preset.name + "/" + input.name +
                                " first=" + firstPosition + " second=" + content.anchoredPosition);
                            AssertFixed();
                        }
                        var drag = new PointerEventData(events) { delta = new Vector2(0, 100000) };
                        keyboard.OnBeginDrag(drag);
                        keyboard.OnDrag(drag);
                        var maximum = content.anchoredPosition;
                        keyboard.OnDrag(drag);
                        Require(Vector2.Distance(maximum, content.anchoredPosition) < .1f, "Upward drag must stop at a finite boundary.");
                        Require(!float.IsInfinity(maximum.y) && !float.IsNaN(maximum.y) && maximum.y >= origin.y,
                            "Form offset must stay finite and above its authored position.");
                        float bodyBottom = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, Corners(keyboard.panelSizeRect)[0]).y;
                        Require(bodyBottom >= keyboardHeight - .5f && bodyBottom < height,
                            preset.name + ": bottom of the form should remain above keyboard and within the viewport: bottom=" +
                            bodyBottom + " keyboard=" + keyboardHeight + " viewport=" + height + " offset=" + maximum);
                        AssertFixed();
                        drag.delta = new Vector2(0, -100000);
                        keyboard.OnDrag(drag);
                        Require(Vector2.Distance(content.anchoredPosition, origin) < .1f, "Downward drag must stop at authored position.");
                        keyboard.OnEndDrag(drag);
                        drag.delta = new Vector2(0, 100000);
                        keyboard.OnDrag(drag);
                        keyboard.OnKeyboardHeightChanged(0);
                        Require(Vector2.Distance(content.anchoredPosition, origin) < .1f, "Keyboard close must restore form even after dragging.");
                        keyboard.OnDrag(drag);
                        Require(Vector2.Distance(content.anchoredPosition, origin) < .1f, "Closed keyboard must disable dragging.");
                        AssertFixed();
                        cases++;
                    }
                    report.AppendLine("PASS 1080x" + height + " " + preset.name + ": focus, extreme drags, fixed backgrounds, close/reset.");

                    void AssertFixed()
                    {
                        Require(Vector2.Distance(pageRoot.anchoredPosition, pageOrigin) < .01f, "Full page must never move.");
                        Require(SameCorners(underlay, underlayCorners), "Opaque input barrier moved.");
                        Require(SameCorners(backdrop, backdropCorners), "PayPal backdrop moved.");
                        Require(SameCorners(pixBackdrop, pixCorners), "PIX/PagBank backdrop moved.");
                        Require(SameCorners(danaBackdrop, danaCorners), "DANA/OVO backdrop moved.");
                    }
                }
                report.AppendLine("Keyboard/drag cases=" + cases);
                events.SetSelectedGameObject(null);
            }
            finally { if (events != null) events.ReleaseForInspection(); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void InspectService(StringBuilder report)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            KeyboardInspectionEventSystem events = null;
            try
            {
                var canvas = MakeCanvas(scene, 2400);
                events = new GameObject("ServiceInspectionEvents", typeof(KeyboardInspectionEventSystem)).GetComponent<KeyboardInspectionEventSystem>();
                SceneManager.MoveGameObjectToScene(events.gameObject, scene);
                events.ActivateForInspection();
                EventSystem.current = events;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Service), scene);
                go.transform.SetParent(canvas.transform, false);
                var keyboard = go.GetComponent<KeyBoardPanel>();
                var data = new SerializedObject(keyboard);
                var panel = (RectTransform)data.FindProperty("panel").objectReferenceValue;
                data.FindProperty("transitionTime").floatValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                var origin = panel.anchoredPosition;
                Require(!keyboard.dragable, "Service keyboard panel should keep dragging disabled.");
                var field = panel.GetComponentInChildren<AdvancedInputField>(true);
                Require(field != null, "Service input must belong to movable keyboard content.");
                events.SetSelectedGameObject(field.gameObject);
                keyboard.OnKeyboardHeightChanged(1000);
                var focused = panel.anchoredPosition;
                keyboard.OnDrag(new PointerEventData(events) { delta = new Vector2(0, 100000) });
                Require(Vector2.Distance(focused, panel.anchoredPosition) < .1f, "Service must still reject dragging.");
                keyboard.OnKeyboardHeightChanged(0);
                Require(Vector2.Distance(origin, panel.anchoredPosition) < .1f, "Service input must restore after keyboard close.");
                events.SetSelectedGameObject(null);
                report.AppendLine("PASS shared service input: descendant binding, disabled drag, close/reset.");
            }
            finally { if (events != null) events.ReleaseForInspection(); EditorSceneManager.ClosePreviewScene(scene); }
        }

        static Canvas MakeCanvas(Scene scene, int height)
        {
            var cam = new GameObject("GeometryCamera", typeof(Camera)).GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(cam.gameObject, scene);
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = height / 2f;
            cam.pixelRect = new Rect(0, 0, 1080, height);
            cam.transform.position = new Vector3(0, 0, -1000);
            var canvas = new GameObject("GeometryCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            SceneManager.MoveGameObjectToScene(canvas.gameObject, scene);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, height);
            return canvas;
        }

        static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners;
        }

        static bool SameCorners(RectTransform rect, Vector3[] expected)
        {
            var actual = Corners(rect);
            for (int i = 0; i < 4; i++) if (Vector3.Distance(actual[i], expected[i]) > .01f) return false;
            return true;
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
