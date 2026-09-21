from pathlib import Path
import re

ROOT = Path('BizzaWZ/Assets/BubblePics/Scripts')

def body(text, signature, replacement):
    start = text.index(signature)
    left = text.index('{', start)
    depth, right = 1, left + 1
    while depth:
        depth += (text[right] == '{') - (text[right] == '}')
        right += 1
    return text[:left] + '{\n' + replacement + '\n        }' + text[right:]

def edit(path, action):
    p = ROOT / path
    p.write_text(action(p.read_text(encoding='utf-8-sig')), encoding='utf-8')

def app(text):
    text = re.sub(r'\s*\[RuntimeInitializeOnLoadMethod\(RuntimeInitializeLoadType.AfterSceneLoad\)\]\s*static void Boot\(\).*?(?=        void Awake\(\))', '\n', text, flags=re.S)
    text = body(text, 'void Awake()', '''            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            DeviceLayout.RefreshFromScreen();
            if (_prefabCatalog == null || _cam == null || _worldRoot == null)
                throw new System.InvalidOperationException("AppRoot is missing authored gameplay references.");
            PrefabCatalog.SetCurrent(_prefabCatalog);
            RemoteImages = new BubblePicsRemoteImageDelivery(this);
            Time.fixedDeltaTime = 1f / 60f;
            Physics2D.gravity = new Vector2(0, -980f);
            Physics2D.velocityIterations = 32;
            Physics2D.positionIterations = 16;
            Physics2D.maxLinearCorrection = 6f;
            Physics2D.maxTranslationSpeed = 5000f;
            SoundManager.Create();
            SetupCanvases();
            _cam.orthographicSize = DeviceLayout.ViewHeight / 2f;
            SetPresentationVisible(false);''')
    a = text.index('        // ------------------------------------------------------------ launch flow')
    b = text.index('        /// <summary>\n        /// Instantiates one catalog entry', a)
    text = text[:a] + '''        public void InitializeGameplay()
        {
            if (Page != null) return;
            Page = InstantiateMounted<BubblePage>(Prefabs.BubblePage);
            if (Page == null) throw new System.InvalidOperationException("BubblePage prefab is missing.");
            Page.InitializePrefabRuntime(this);
            Page.Input.InputEnabled = false;
            SplashOwnsLevelLoading = true;
            RemoteImages.BeginImageIndexWarmup();
        }

        public void SetPresentationVisible(bool visible)
        {
            _cam.enabled = visible;
            _hudCanvas.enabled = visible;
            _panelCanvas.enabled = visible;
            _dialogCanvas.enabled = visible;
            _splashCanvas.enabled = false;
        }

        public void MountGameplayUi(RealGamePanel panel)
        {
            MountCanvas(_hudRoot, panel.content);
            MountCanvas(_panelRoot, panel.content);
            MountCanvas(_dialogRoot, panel.content);
            ApplyDeviceLayout();
        }

        void MountCanvas(RectTransform root, Transform parent)
        {
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            FitCanvas(root);
        }

        void FitCanvas(RectTransform root)
        {
            if (root == null || root.parent == transform) return;
            Canvas canvas = root.GetComponentInParent<Canvas>().rootCanvas;
            root.sizeDelta = new Vector2(DeviceLayout.ViewWidth, DeviceLayout.ViewHeight);
            root.localScale = Vector3.one * (DeviceLayout.PixelsPerDesignUnit / canvas.scaleFactor);
        }

        public HomePage EnsureHomePage() => throw new System.InvalidOperationException("Gameplay navigation is owned by the framework.");
        public LoadingOverlay ShowLoading() => throw new System.InvalidOperationException("Use the framework loading lifecycle.");
        public void HideLoading() { BizzaGameplayBridge.OnLevelAssetsReady(); }

''' + text[b:]
    text = text.replace('_cam.orthographicSize = DeviceLayout.ViewHeight / 2f;\n        }', '_cam.orthographicSize = DeviceLayout.ViewHeight / 2f;\n            FitCanvas(_hudRoot); FitCanvas(_panelRoot); FitCanvas(_dialogRoot);\n        }')
    # Legacy dynamic camera construction is not part of the framework bootstrap.
    a = text.index('        void SetupCamera()')
    b = text.index('        void SetupCanvases()', a)
    text = text[:a] + text[b:]
    return text

edit('App/App.cs', app)

def page(text):
    text = text.replace('            if (IsTutorialRound()) Tutorial.Begin();', '            // The framework EnterCustomTutorial node starts the base tutorial.')
    text = text.replace('public void EmitRoundStarted() { RoundStartedEvent?.Invoke(); }', 'public void EmitRoundStarted() { RoundStartedEvent?.Invoke(); BizzaGameplayBridge.OnRoundStarted(); }')
    text = text.replace('            RoundSeq++;', '            BizzaGameplayBridge.OnRoundClearing();\n            RoundSeq++;')
    text = text.replace('LoadingOverlay loading =\n                App.I != null && !App.I.SplashOwnsLevelLoading\n                    ? App.I.ShowLoading()\n                    : null;', 'LoadingOverlay loading = null; // Framework owns the loading page.')
    text = text.replace('                App.I?.EnsureHomePage()?.Show();', '                BizzaGameplayBridge.OnLoadFailed();')
    text = body(text, 'public void RestartLevel()', '            FlowModule.LoadGameLevel();')
    text = body(text, 'public void RequestGoHome()', '            FlowModule.LoadGameLevel();')
    text = body(text, 'public void GoHome(bool discardProgress = false)', '            FlowModule.LoadGameLevel();')
    text = body(text, 'void FinalizeRound()', '''            if (_persisted) return;
            RecordRoundCoefficient(false);
            _persisted = true;
            SaveState.RoundSnapshot = "";
            _pendingReward = 0;
            _roundTelemetryActive = false;
            Specials?.OnRoundWon(CurrentLevelNumber);''')
    text = body(text, 'void PresentCompletion()', '            BizzaGameplayBridge.Win();')
    text = body(text, 'public void OnCompleteNext()', '            FlowModule.LoadGameLevel();')
    text = body(text, 'void ContinueAfterCompletion()', '            FlowModule.LoadGameLevel();')
    text = body(text, 'public void ShowBombDeathUi()', '''            if (!_dead || !_bombDeath || _persisted || Fly.IsFlying || _awarding) return;
            TopBar.Dolphin.PlayFail();
            BizzaGameplayBridge.Lose(false);''')
    text = body(text, 'public void RequestDeathUi()', '''            if (!_dead || _bombDeath || _persisted || Fly.IsFlying || _awarding) return;
            if (IsMovesUnlimited()) { _dead = false; return; }
            TopBar.Dolphin.PlayFail();
            BizzaGameplayBridge.Lose(true);''')
    text = body(text, 'public void OnContinueAd()', '            BizzaGameplayBridge.Lose(!_bombDeath);')
    text = body(text, 'public void OnContinueCoin()', '            BizzaGameplayBridge.Lose(!_bombDeath);')
    text = body(text, 'void PlayContinueRevive(string reviveType)', '''            _stepRule.Revive(15);
            _reviveCount++;
            _continueAdCount++;
            _dead = false;
            _bombDeath = false;
            TopBar.Dolphin.ResumeIdle();
            TopBar.SetMoves(_stepRule.StepsLeft);
            SaveRoundSnapshot();''')
    # Framework callbacks are generation-checked before this applies the original effect.
    text = text.replace('        void PlayContinueRevive(string reviveType)', '        public void ApplyFrameworkRevive() => PlayContinueRevive("ad");\n\n        void PlayContinueRevive(string reviveType)')
    text = text.replace('            CollectedImgs.Add(imageId);', '            CollectedImgs.Add(imageId);')
    text = text.replace('public void AddCollectedImage(int imageId) { CollectedImgs.Add(imageId); }', 'public void AddCollectedImage(int imageId) { CollectedImgs.Add(imageId); FlowModule.SynthesisLogic(Mathf.Max(0, PickedTextures.Length - CollectedImgs.Count)); }')
    return text

edit('Game/BubblePage.cs', page)

def saves(text):
    text = text.replace('get => PlayerPrefs.GetInt(P + "current_level", 1);', 'get => SaveDataUtils.GameData.playerSelectedLv;')
    text = text.replace('set { PlayerPrefs.SetInt(P + "current_level", value); PlayerPrefs.Save(); }', 'set { SaveDataUtils.GameData.playerSelectedLv = value; SaveDataUtils.Save(); }')
    text = text.replace('get => PlayerPrefs.GetInt(P + "sound_on", 1) == 1;', 'get => SaveDataUtils.SettingData.enableSound;')
    text = text.replace('set { PlayerPrefs.SetInt(P + "sound_on", value ? 1 : 0); PlayerPrefs.Save(); }', 'set { SaveDataUtils.SettingData.enableSound = value; SaveDataUtils.Save(); }')
    text = text.replace('get => PlayerPrefs.GetInt(P + "vibrate_on", 1) == 1;', 'get => SaveDataUtils.SettingData.enableVibrate;')
    text = text.replace('set { PlayerPrefs.SetInt(P + "vibrate_on", value ? 1 : 0); PlayerPrefs.Save(); }', 'set { SaveDataUtils.SettingData.enableVibrate = value; SaveDataUtils.Save(); }')
    text = body(text, 'public static int GetToolCount(string tool)', '            return ItemUtils.GetItemCount(BizzaGameplayBridge.ToolType(tool));')
    text = body(text, 'public static void SetToolCount(string tool, int v)', '''            var type = BizzaGameplayBridge.ToolType(tool);
            int delta = v - ItemUtils.GetItemCount(type);
            if (delta > 0) ItemUtils.AddItem(type, delta);
            else if (delta < 0) throw new System.InvalidOperationException("Only framework UI may consume gameplay props.");''')
    return text

edit('App/SaveState.cs', saves)
for p in ROOT.rglob('*.cs'):
    if p.name == 'GameplayPreferences.cs': continue
    text = p.read_text(encoding='utf-8-sig').replace('PlayerPrefs.', 'GameplayPreferences.')
    # Imported build flavors have no role in this formal integration.
    text = text.replace('BuildFlavor.IsWhitePackage', 'false').replace('BuildFlavor.AdvertisingEnabled', 'true')
    p.write_text(text, encoding='utf-8')

edit('Game/Tutorial.cs', lambda s: s.replace('            SaveState.TutorialDone = true;', '            SaveState.TutorialDone = true;\n            BizzaGameplayBridge.CompleteBaseTutorial();'))
edit('Game/InputController.cs', lambda s: s.replace('if (!InputEnabled || Page == null || app == null || app.Cam == null)', 'if (!InputEnabled || Page == null || app == null || app.Cam == null || BizzaGameplayBridge.IsInputBlocked)'))
edit('App/ResourceAssetLoader.cs', lambda s: s.replace('                asset = LoadOptionalEditorAsset<T>(resourcePath);', '                asset = null; // No Editor-only resource fallback.'))
print('Gameplay lifecycle and framework save routing adapted.')
