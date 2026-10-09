using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Disposable prefab preview. Uses production placement methods, without opening account pages.
    public static class WithdrawalGuidePreview
    {
        const string TeachRoot = "Assets/BizzaWZ/Final/Framework/Runtime/Module/Teach/UITeach/";
        public static void Render(string folder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder("Prefab geometry/render inspection; no gameplay, payout or save flows invoked.\n");
            foreach (int height in new[] {2400, 1920})
            {
                var scene = EditorSceneManager.NewPreviewScene();
                var texture = new RenderTexture(1080, height, 24);
                var previous = RenderTexture.active;
                try
                {
                    var cameraObject = new GameObject("Preview camera", typeof(Camera));
                    SceneManager.MoveGameObjectToScene(cameraObject, scene);
                    var camera = cameraObject.GetComponent<Camera>();
                    camera.scene = scene; camera.orthographic = true; camera.orthographicSize = height * .5f;
                    camera.aspect = 1080f / height; camera.transform.position = new Vector3(0, 0, -100);
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f,.12f,.26f);
                    camera.targetTexture = texture;
                    var canvasObject = new GameObject("Preview canvas", typeof(Canvas), typeof(GraphicRaycaster));
                    SceneManager.MoveGameObjectToScene(canvasObject, scene);
                    var canvas = canvasObject.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                    ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, height);
                    var eventObject = new GameObject("Preview event system", typeof(EventSystem));
                    SceneManager.MoveGameObjectToScene(eventObject, scene);
                    var withdrawal = Spawn("Assets/BizzaWZ/Final/Real/UI/FakeWithdrawPanel/FakeWithdrawPanel.prefab");
                    var hud = Spawn("Assets/BizzaWZ/Final/Real/UI/GamePanel/Resources/Resources/GameUiWidget.prefab");
                    var mask = Spawn(TeachRoot + "UITeachMaskPage.prefab").GetComponent<UITeachMaskPage>();
                    var tip = Spawn(TeachRoot + "UITeachTipsPage.prefab").GetComponent<UITeachTipsPage>();
                    mask.imgMask.enabled = false; mask.block.color = new Color(0,0,0,100f/255f);
                    mask.finger.gameObject.SetActive(false);
                    tip.bg.color = Color.clear; tip.bg.raycastTarget = false;
                    Capture(withdrawal, "withdraw-starter", "Clique aqui para sacar", "withdraw");
                    Capture(hud, "withdraw-cash", "Clique aqui para sacar", "cash-entry");
                    Capture(hud, "withdraw-coins", "Troque moedas por dinheiro", "coin-entry");

                    GameObject Spawn(string path)
                    {
                        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        root.transform.SetParent(canvas.transform, false);
                        foreach (var source in root.GetComponentsInChildren<CoralResourceSprite>(true))
                        {
                            var data = new SerializedObject(source);
                            source.SetSource(data.FindProperty("_resourcePath").stringValue, data.FindProperty("_spriteName").stringValue);
                        }
                        return root;
                    }
                    void Capture(GameObject page, string marker, string message, string name)
                    {
                        withdrawal.SetActive(page == withdrawal); hud.SetActive(page == hud);
                        Button button = null;
                        foreach (var item in page.GetComponentsInChildren<GuideObjectMarker>(true))
                            if (item.GUID == marker) button = item.GetComponent<Button>();
                        if (button == null || button.targetGraphic.gameObject != button.gameObject)
                            throw new InvalidOperationException("Missing visible Button " + marker);
                        var target = (RectTransform)button.transform;
                        Canvas.ForceUpdateCanvases();
                        mask.FocusTarget(target);
                        mask.GetComponent<TutorialFocusBackdrop>().RefreshLayout();
                        tip.content.text = message;
                        tip.FollowHighlight(mask);
                        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(false)) text.ForceMeshUpdate(true, true);
                        Canvas.ForceUpdateCanvases();
                        Validate();
                        var position = target.anchoredPosition;
                        target.anchoredPosition += Vector2.up * 35;
                        mask.FocusTarget(target); tip.FollowHighlight(mask); Validate();
                        target.anchoredPosition = position;
                        var scale = target.parent.localScale;
                        target.parent.localScale = scale * .82f;
                        mask.FocusTarget(target); tip.FollowHighlight(mask); Validate();
                        target.parent.localScale = scale;
                        mask.FocusTarget(target); tip.FollowHighlight(mask);
                        mask.GetComponent<TutorialFocusBackdrop>().RefreshLayout();
                        Canvas.ForceUpdateCanvases();
                        camera.Render(); RenderTexture.active = texture;
                        var pointer = new PointerEventData(eventObject.GetComponent<EventSystem>()) { position = Bounds(target,camera).center };
                        var hits = new List<RaycastResult>();
                        canvasObject.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                        if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != button)
                            throw new InvalidOperationException("Target Button obscured: " + marker + " first=" + (hits.Count>0?hits[0].gameObject.name:"none"));
                        report.AppendLine("PASS standard UI raycast reaches target Button: " + marker);
                        var image = new Texture2D(1080,height,TextureFormat.RGB24,false);
                        try
                        {
                            image.ReadPixels(new Rect(0,0,1080,height),0,0); image.Apply();
                            File.WriteAllBytes(Path.Combine(folder,name + "-1080x" + height + ".png"),image.EncodeToPNG());
                        }
                        finally { UnityEngine.Object.DestroyImmediate(image); }
                        void Validate()
                        {
                            var expected = Bounds(target, camera); var actual = Bounds(mask.maskParent, camera);
                            var bubble = Bounds(tip.panel.rectTransform, camera);
                            if (Vector2.Distance(expected.center,actual.center)>1 || Vector2.Distance(expected.size,actual.size)>1)
                                throw new InvalidOperationException("Highlight mismatch: " + marker);
                            if (bubble.Overlaps(expected) || bubble.xMin<0 || bubble.xMax>1080 || bubble.yMin<0 || bubble.yMax>height)
                                throw new InvalidOperationException("Tip overlaps button/viewport: " + marker + " " + bubble + " button=" + expected);
                            if (tip.content.isTextOverflowing) throw new InvalidOperationException("Text overflow: " + marker);
                            if (tip.content.transform.lossyScale.x<0 || tip.content.transform.lossyScale.y<0)
                                throw new InvalidOperationException("Mirrored text: " + marker);
                            report.AppendLine("PASS " + marker + " 1080x" + height + " highlight=" + actual + " tip=" + bubble);
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                    RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
                    File.WriteAllText(Path.Combine(folder,"prefab-inspection.txt"),report.ToString());
                }
            }
        }
        static Rect Bounds(RectTransform item, Camera camera)
        {
            var corners = new Vector3[4]; item.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue,float.MaxValue), max = -min;
            foreach(var corner in corners) { Vector2 p=camera.WorldToScreenPoint(corner); min=Vector2.Min(min,p);max=Vector2.Max(max,p); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
