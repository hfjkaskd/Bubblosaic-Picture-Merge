using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics.GameModes
{
    public interface IModeLevelPreparationHandler
    {
        GameplayKind Kind { get; }

        IEnumerator Prepare(
            LevelModeSelection selection,
            Action<ModePreparedLevel> completed,
            Action<string> failed,
            Action<float> progress);
    }

    public static class ModeLevelEntry
    {
        sealed class BuiltinPreparationHandler : IModeLevelPreparationHandler
        {
            public BuiltinPreparationHandler(GameplayKind kind)
            {
                Kind = kind;
            }

            public GameplayKind Kind { get; }

            public IEnumerator Prepare(
                LevelModeSelection selection,
                Action<ModePreparedLevel> completed,
                Action<string> failed,
                Action<float> progress)
            {
                return ModeLevelPreparer.Prepare(
                    selection,
                    completed,
                    failed,
                    progress);
            }
        }

        static readonly Dictionary<GameplayKind, IModeLevelPreparationHandler>
            Handlers = new Dictionary<GameplayKind, IModeLevelPreparationHandler>();
        static readonly Dictionary<BubblePage, Coroutine> Pending =
            new Dictionary<BubblePage, Coroutine>();
        static bool _initialized;

        public static void RegisterPreparationHandler(
            IModeLevelPreparationHandler handler)
        {
            if (handler != null) Handlers[handler.Kind] = handler;
        }

        public static void UnregisterPreparationHandler(GameplayKind kind)
        {
            Handlers.Remove(kind);
        }

        public static bool TryOpenAutomatic(
            BubblePage page,
            int globalLevel,
            string backgroundEntry,
            float entryMaskAlpha)
        {
            EnsureInitialized();
            if (!LevelModeRouter.TryResolveAutomatic(
                    globalLevel,
                    out LevelModeSelection selection) ||
                !Handlers.ContainsKey(selection.Kind))
                return false;
            Begin(
                page,
                selection,
                backgroundEntry,
                entryMaskAlpha,
                fallbackToMain: true);
            return true;
        }

        public static bool TryOpenExplicit(
            BubblePage page,
            GameplayKind kind,
            int globalLevel,
            string backgroundEntry = "fresh",
            float entryMaskAlpha = 0f)
        {
            EnsureInitialized();
            if (!LevelModeRouter.TryResolveExplicit(
                    kind,
                    globalLevel,
                    out LevelModeSelection selection) ||
                !Handlers.ContainsKey(selection.Kind))
                return false;
            Begin(
                page,
                selection,
                backgroundEntry,
                entryMaskAlpha,
                fallbackToMain: false);
            return true;
        }

        static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            BuiltinModeSelectors.EnsureRegistered();
            RegisterPreparationHandler(
                new BuiltinPreparationHandler(GameplayKind.CategoryMatch));
            RegisterPreparationHandler(
                new BuiltinPreparationHandler(GameplayKind.WordMatch));
            RegisterPreparationHandler(
                new BuiltinPreparationHandler(GameplayKind.Tangram));
            RegisterPreparationHandler(
                new BuiltinPreparationHandler(GameplayKind.Bonus));
        }

        static void Begin(
            BubblePage page,
            LevelModeSelection selection,
            string backgroundEntry,
            float entryMaskAlpha,
            bool fallbackToMain)
        {
            if (page == null) return;
            if (Pending.TryGetValue(page, out Coroutine old) && old != null)
                page.StopCoroutine(old);
            Pending[page] = page.StartCoroutine(OpenCo(
                page,
                selection,
                backgroundEntry,
                entryMaskAlpha,
                fallbackToMain));
        }

        static IEnumerator OpenCo(
            BubblePage page,
            LevelModeSelection selection,
            string backgroundEntry,
            float entryMaskAlpha,
            bool fallbackToMain)
        {
            LoadingOverlay loading = App.I != null &&
                                     !App.I.SplashOwnsLevelLoading
                ? App.I.ShowLoading()
                : null;
            loading?.SetProgress(0f);
            ModePreparedLevel prepared = null;
            string error = null;
            IModeLevelPreparationHandler handler = Handlers[selection.Kind];
            yield return handler.Prepare(
                selection,
                value => prepared = value,
                message => error = message,
                value => loading?.SetProgress(value));
            Pending.Remove(page);

            if (prepared == null || !string.IsNullOrWhiteSpace(error))
            {
                loading?.Hide();
                Debug.LogWarning(
                    $"{selection.Kind} level {selection.GlobalLevel} is not " +
                    $"deliverable; {(fallbackToMain ? "using Main" : "preview cancelled")}: " +
                    (error ?? "unknown preparation error"));
                if (fallbackToMain)
                {
                    ModeSession.Clear();
                    page.OpenMainLevel(
                        selection.GlobalLevel,
                        backgroundEntry,
                        entryMaskAlpha);
                }
                else
                {
                    Toast.Show(Localization.Tr("NET_ERROR_LOADING_FAILED"));
                }
                yield break;
            }

            page.OpenPreparedModeLevel(
                selection,
                prepared,
                backgroundEntry,
                entryMaskAlpha);
        }
    }
}
