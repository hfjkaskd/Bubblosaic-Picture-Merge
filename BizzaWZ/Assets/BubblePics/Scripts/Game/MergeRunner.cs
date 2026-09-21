using System.Collections;
using System.Collections.Generic;
using BubblePics.GameModes;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Port of bubble_merge_runner.gd, production branch (merge_animation group1:
    /// 30fps keyframes F25/F35/F37/F42 with x4 phase-1 speed).
    /// New closure effects enabled (complete_animation new).
    /// </summary>
    public class MergeRunner : MonoBehaviour
    {
        public const float FUSION_DURATION = 0.45f;
        public const float FUSION_RINGDOWN = 0.26f;
        public const float SEAM_OVERSCAN_EXP = 0.025f;
        public const float YES2_MERGE_DIP = 0.8f;
        public static float CLOSURE_FUSION_MULT = 0.5f;

        const float _FPS = 30f;
        const float _PHASE1_SPEED_MULT = 4f;
        const float _C_INITIAL_SCALE = 1.0f;
        const float _C_KEYFRAME_35 = 1.15f;
        const float _C_KEYFRAME_37 = 0.95f;
        const float _C_KEYFRAME_42 = 1.0f;
        const float _HALO_KEYFRAME_42 = 1.2f;

        public BubblePage Page;

        public bool Merging { get; private set; }
        readonly HashSet<BubbleView> _mergeTargets = new HashSet<BubbleView>();
        readonly HashSet<BubbleView> _mergeTargetsLocked = new HashSet<BubbleView>();
        BlockSizeController _blockSize;

        public bool IsBusy => Merging || _mergeTargets.Count > 0;

        public bool HasTargetConflict(BubbleView src, BubbleView target)
            => _mergeTargets.Contains(src) || _mergeTargets.Contains(target)
            || _mergeTargetsLocked.Contains(src) || _mergeTargetsLocked.Contains(target);

        public bool IsMergingTarget(BubbleView b) => _mergeTargets.Contains(b);

        void Awake()
        {
            EnsureBlockSizeController();
        }

        void Start()
        {
            EnsureBlockSizeController();
        }

        void EnsureBlockSizeController()
        {
            if (_blockSize == null)
                _blockSize = GetComponent<BlockSizeController>();
            if (_blockSize == null)
                _blockSize = gameObject.AddComponent<BlockSizeController>();
            _blockSize.Initialize(Page != null ? Page : GetComponent<BubblePage>());
        }

        public void ResetAll()
        {
            Merging = false;
            _mergeTargets.Clear();
            _mergeTargetsLocked.Clear();
        }

        class PieceAnim
        {
            public BubbleView.PieceInfo Piece;
            public bool IsIncoming;
            public Vector3 StartPos;    // world
            public float StartRot;
            public Vector3 StartScale;  // lossy (world) scale
        }

        static bool UseNewClosureEffects(bool isLastImageOfRound, bool isNumber)
        {
            if (isNumber) return false;
            return AppConfig.CompleteAnimationNew;
        }

        static bool UseRainbowShine(bool isLastImageOfRound, bool isNumber)
        {
            if (isNumber) return false;
            return isLastImageOfRound && AppConfig.LastLinkEnhance;
        }

        float Yes2MergeDip(float lt, bool skipClosure)
        {
            if (skipClosure) return 1f;
            return 1f - (1f - YES2_MERGE_DIP) * Mathf.Sin(lt * Mathf.PI);
        }

        public IEnumerator Run(BubbleView src, BubbleView target, bool fromTool = false)
        {
            if (src == null || target == null) yield break;
            EnsureBlockSizeController();
            yield return RunExperimentPair(src, target, fromTool);
        }

        IEnumerator RunExperimentPair(BubbleView src, BubbleView target, bool fromTool)
        {
            int myRound = Page.RoundSeq;
            if (Page.Specials != null && !Page.Specials.ResolveRainbowMerge(src, target))
                yield break;
            bool diagonalPerfect = AppConfig.DiagonalPerfectFx &&
                BubbleSpecialRules.IsDiagonalPerfect(src.Fragment, target.Fragment);
            Merging = true;
            float mult = 1f;
            BubbleView.MuteCollideFor((FUSION_DURATION + FUSION_RINGDOWN) * mult + 0.5f);
            src.MarkMerging();
            _mergeTargets.Add(target);

            src.CancelPickupEnlargeForMerge();
            target.CancelPickupEnlargeForMerge();

            var resultHeld = BubbleView.MergeResult(target.Fragment, src.Fragment.HeldPaths);
            bool tangramMoldMerge =
                TangramMatchContent.IsTangramImage(target.ImageId) &&
                (target.Fragment.TangramMold || src.Fragment.TangramMold);
            bool categoryTarget =
                CategoryMatchContent.IsCategoryImage(target.ImageId);
            bool isClosure = resultHeld.Count == 1 &&
                             resultHeld[0].Count == 0;
            bool isUpgrade = isClosure ||
                             (resultHeld.Count == 1 && !tangramMoldMerge);
            if (isUpgrade) _mergeTargetsLocked.Add(target);

            int nFinal;
            var mq = new List<int>();
            if (isUpgrade && !isClosure) nFinal = 1;
            else
            {
                if (isUpgrade) mq = new List<int> { 0, 1, 2, 3 };
                else
                {
                    foreach (var p in resultHeld)
                        if (p.Count > 0) mq.Add(p[p.Count - 1]);
                }
                nFinal = Mathf.Clamp(
                    mq.Count > 0
                        ? BubbleView.RadiusBlockCount(
                            mq,
                            target.UsesCompactFormation())
                        : 1,
                    1,
                    4);
            }

            if (!tangramMoldMerge)
                MaybeApplyBlockSize(target, nFinal);
            var layoutQuads = isUpgrade ? new List<int> { 0, 1, 2, 3 } : mq;

            src.BubbleSprite.gameObject.SetActive(true);
            target.BubbleSprite.gameObject.SetActive(true);
            src.ClearSelectGlowInstant();
            target.ClearSelectGlowInstant();
            target.ZIndex = 11;
            src.ZIndex = 12;

            // The steady jigsaw visual is a merged silhouette. Restore its
            // individual quadrant meshes before capturing world transforms so
            // the normal fusion runner can animate each tab into place.
            if (JigsawChipRule.IsEnabled)
            {
                target.RebuildImagesJigsawAnimatable();
                src.RebuildImagesJigsawAnimatable();
            }

            // capture pieces of both bubbles (world transforms)
            var pieces = new List<PieceAnim>();
            foreach (var b in new[] { src, target })
            {
                foreach (var pi in b.Pieces)
                {
                    if (pi.Go == null || !pi.Animate) continue;
                    pieces.Add(new PieceAnim
                    {
                        Piece = pi,
                        IsIncoming = b == src,
                        StartPos = pi.Go.transform.position,
                        StartRot = pi.Go.transform.eulerAngles.z,
                        StartScale = pi.Go.transform.lossyScale,
                    });
                    // re-mask to final layout
                    if (!tangramMoldMerge && !categoryTarget && pi.Sr != null)
                    {
                        var mat = pi.Sr.material;
                        int em = BubbleView.ComputeEdgeMask(pi.Quadrant, layoutQuads, false);
                        int cm = BubbleView.ComputeCornerMask(pi.Quadrant, layoutQuads, false);
                        int fm = BubbleView.FillCornerMaskForPiece(pi.Quadrant, layoutQuads, false);
                        mat.SetFloat("_EdgeMask", em);
                        mat.SetFloat("_CornerMask", cm);
                        mat.SetFloat("_FillCornerMask", fm & cm);
                    }
                }
            }

            target.MergeWithDataOnly(src);
            bool completedGroup = tangramMoldMerge
                ? isClosure
                : resultHeld.Count == 1;
            bool auxiliaryClosure = target.IsFull() &&
                Page.Specials != null && Page.Specials.IsAuxiliaryClosure(target);

            int closureSlotIdx = -1;
            bool isLastImageOfRound = !auxiliaryClosure && target.IsFull() &&
                (Page.TopBar.ReservedCount + 1 >= Page.TopBar.SlotCount);
            // The normal closure showcase marks the target Exploding but keeps
            // it alive for the collection flight. A magnet closure owns its
            // own burst/destruction sequence, so applying both would make the
            // later Explode call return early and leave the dolphin on board.
            bool useNewClosureFx = isClosure && !auxiliaryClosure &&
                UseNewClosureEffects(isLastImageOfRound, false);
            if (target.IsFull() && !auxiliaryClosure)
            {
                closureSlotIdx = Page.TopBar.ReserveNextIndex();
                if (target.Fragment != null) Page.AddCollectedImage(target.Fragment.ImageId);
                if (isLastImageOfRound &&
                    (Page.Specials == null || Page.Specials.CanCompleteRound))
                    Page.EmitRoundWon();
                Page.EmitImageClosurePending(target.Fragment?.ImageId ?? -1, closureSlotIdx);
            }

            bool deferNextWave = Page.TakeDeferNextWaveFlag();
            if (completedGroup && Page.HasPendingTokens() && !deferNextWave)
                Page.ConsumeNextWave();
            Page.CommitDeath();

            // Every successful merge is eligible, including partial pictures. Settlement wins priority.
            if (!isLastImageOfRound && !Page.IsDead())
                FlowModule.SynthesisLogic(Mathf.Max(0, Page.PickedTextures.Length - Page.CollectedImgs.Count), target.transform.position);

            Page.EmitMergeStarted(target, isUpgrade, isClosure, completedGroup);
            Fx.Vibrate(1); // WEAK

            bool numberTarget = NumberMatchContent.IsNumberImage(target.ImageId);
            bool wordTarget = WordMatchContent.IsWordImage(target.ImageId);
            bool tangramTarget =
                TangramMatchContent.IsTangramImage(target.ImageId);
            float cellMerged = tangramTarget
                ? TangramMatchContent.FormationScale(
                    target.ImageId,
                    layoutQuads,
                    target.PhotoRadius(),
                    target.Fragment.TangramMold)
                : categoryTarget
                    ? CategoryMatchContent.FormationCell(
                        layoutQuads, target.PhotoRadius(), nFinal)
                : numberTarget || wordTarget
                    ? NumberMatchContent.FormationCell(
                        layoutQuads, target.PhotoRadius(), nFinal)
                    : BubbleView.QuadrantCell(
                        layoutQuads, target.PhotoRadius(), nFinal);
            float fusionEnlarge = 1.0f;
            bool committedFired = false;

            if (useNewClosureFx)
                target.PlayClosureEffectsOnly(UseRainbowShine(isLastImageOfRound, false));

            yield return ExperimentAnimation(src, target, v => committedFired = v, () => committedFired,
                isUpgrade, isClosure, completedGroup, myRound, pieces, layoutQuads, cellMerged, fusionEnlarge,
                fromTool, AppConfig.MergeAnimationGroup1, useNewClosureFx);

            if (myRound < Page.RoundSeq)
            {
                Merging = false;
                _mergeTargets.Remove(target);
                _mergeTargetsLocked.Remove(target);
                yield break;
            }

            bool targetOk = target != null && (target.State == BubbleState.Alive
                || (isClosure && target.State == BubbleState.Exploding));
            if (!targetOk)
            {
                if (src != null) Destroy(src.gameObject);
                Merging = false;
                _mergeTargets.Remove(target);
                _mergeTargetsLocked.Remove(target);
                yield break;
            }

            if (!target.Dragging) target.ZIndex = 0;

            if (!useNewClosureFx)
                target.BubbleSprite.gameObject.SetActive(true);
            if (src != null)
            {
                if (useNewClosureFx)
                {
                    var srcRef = src;
                    TweenRunner.Delay(BubbleView.CLOSURE_PRE_HIDE_FRAMES / 60f + 0.02f, () =>
                    {
                        if (srcRef != null)
                        {
                            srcRef.VisualRoot.gameObject.SetActive(false);
                            Destroy(srcRef.gameObject);
                        }
                    });
                }
                else
                {
                    src.VisualRoot.gameObject.SetActive(false);
                    Destroy(src.gameObject);
                }
            }

            if (diagonalPerfect && target != null)
                target.PlayPerfectFitFeedback();
            if (isClosure && target != null)
            {
                if (auxiliaryClosure) Page.Specials.ResolveAuxiliaryClosure(target);
                else RunClosureCollect(target, closureSlotIdx);
            }

            Merging = false;
            _mergeTargets.Remove(target);
            _mergeTargetsLocked.Remove(target);
            Page.EmitMergeResolved();
            _blockSize?.RecomputeAndApply("merge_resolved");
            Page.RequestDeathUi();
        }

        IEnumerator ExperimentAnimation(BubbleView src, BubbleView target,
            System.Action<bool> setCommitted, System.Func<bool> getCommitted,
            bool isUpgrade, bool isClosure, bool completedGroup, int myRound,
            List<PieceAnim> pieces, List<int> layoutQuads, float cellMerged, float fusionEnlarge,
            bool fromTool, bool useEnhancedAnimation, bool useNewClosureFx)
        {
            if (!useEnhancedAnimation)
            {
                yield return ControlAnimation(
                    src, target, setCommitted, getCommitted,
                    isUpgrade, isClosure, completedGroup, myRound,
                    pieces, layoutQuads, cellMerged, fusionEnlarge,
                    fromTool, useNewClosureFx);
                yield break;
            }

            float elapsed = 0f;
            bool cStarted = false;
            GameObject halo = null;
            SpriteRenderer haloSr = null;
            float haloBaseScale = 1f;

            bool useNewClosurePop = useNewClosureFx;
            float closureMult = useNewClosurePop ? CLOSURE_FUSION_MULT : 1f;

            Vector3 glassStartScale = target.BubbleSprite.transform.localScale;
            Vector3 glassEndScale = glassStartScale;
            if (!useNewClosurePop && target.BubbleSprite.sprite != null)
            {
                float glassRVis = target.GetRadius() * BubbleView.GLASS_VISUAL_OVERSCAN;
                float w = target.BubbleSprite.sprite.rect.width;
                float glassS = (2f * glassRVis) / w;
                glassEndScale = new Vector3(glassS, glassS, 1);
            }

            float startR = target.Collider.radius;
            float finalR = target.GetRadius();
            float F25 = 25f / _FPS / _PHASE1_SPEED_MULT * closureMult;
            float F35 = F25 + 10f / _FPS * closureMult;
            float F37 = F35 + (useNewClosurePop ? 0f : 2f / _FPS);
            float F42 = F37 + 5f / _FPS * closureMult;

            while (elapsed < F42)
            {
                yield return null;
                if (myRound < Page.RoundSeq || src == null || target == null)
                {
                    if (halo != null) Destroy(halo);
                    yield break;
                }
                elapsed += Time.deltaTime;
                if (elapsed <= F25)
                {
                    float raw = Mathf.Clamp01(elapsed / F25);
                    float lt = raw * raw * (3f - 2f * raw);
                    float dip = Yes2MergeDip(lt, isClosure);
                    Vector3 targetPos = target.transform.position;

                    var basePos = new Vector3[pieces.Count];
                    for (int i = 0; i < pieces.Count; i++)
                    {
                        var offD = TangramMatchContent.IsTangramImage(target.ImageId)
                            ? TangramMatchContent.FormationOffset(
                                target.ImageId,
                                pieces[i].Piece.Quadrant,
                                layoutQuads,
                                cellMerged,
                                target.Fragment.TangramMold)
                            : CategoryMatchContent.IsCategoryImage(target.ImageId)
                            ? CategoryMatchContent.FormationOffset(
                                pieces[i].Piece.Quadrant,
                                layoutQuads,
                                cellMerged) * fusionEnlarge
                            : NumberMatchContent.IsNumberImage(target.ImageId) ||
                              WordMatchContent.IsWordImage(target.ImageId)
                            ? NumberMatchContent.FormationOffset(
                                pieces[i].Piece.Quadrant,
                                layoutQuads,
                                cellMerged) * fusionEnlarge
                            : BubbleView.QuadrantOffset(
                                pieces[i].Piece.Quadrant,
                                layoutQuads,
                                cellMerged) * fusionEnlarge;
                        var endPos = targetPos + new Vector3(offD.x, -offD.y, 0);
                        basePos[i] = Vector3.Lerp(pieces[i].StartPos, endPos, lt);
                    }
                    Vector3 pivot = Vector3.zero;
                    foreach (var p in basePos) pivot += p;
                    if (basePos.Length > 0) pivot /= basePos.Length;

                    for (int i = 0; i < pieces.Count; i++)
                    {
                        var e = pieces[i];
                        if (e.Piece.Go == null) continue;
                        float pw = e.Piece.RegionWOrig;
                        float endS = TangramMatchContent.IsTangramImage(target.ImageId)
                            ? cellMerged * fusionEnlarge
                            : CategoryMatchContent.IsCategoryImage(target.ImageId)
                            ? CategoryMatchContent.IconDisplaySize(
                                cellMerged,
                                layoutQuads.Count) / pw * fusionEnlarge
                            : cellMerged / pw * fusionEnlarge;
                        float seamBoost = e.IsIncoming ? 1f + SEAM_OVERSCAN_EXP * Mathf.Sin(lt * Mathf.PI) : 1f;
                        e.Piece.Go.transform.position = pivot + (basePos[i] - pivot) * dip;
                        e.Piece.Go.transform.rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(e.StartRot, 0, lt));
                        var s = Vector3.Lerp(e.StartScale, new Vector3(endS, endS, 1), lt) * seamBoost * dip;
                        SetWorldScale(e.Piece.Go.transform, s);
                    }

                    if (src != null && src.BubbleSprite != null)
                    {
                        var c = src.BubbleSprite.color; c.a = 1f - lt; src.BubbleSprite.color = c;
                    }
                    if (!useNewClosurePop && target.BubbleSprite != null)
                        target.BubbleSprite.transform.localScale = Vector3.Lerp(glassStartScale, glassEndScale, lt);
                    target.SetCollisionRadius(Mathf.Lerp(startR, finalR, lt));
                }
                else if (elapsed <= F35)
                {
                    if (!cStarted)
                    {
                        cStarted = true;
                        if (!useNewClosurePop) src.VisualRoot.gameObject.SetActive(false);
                        if (!useNewClosurePop)
                        {
                            target.Refresh();
                            target.ResetImageRotation();
                            target.ResetImageFloat();
                        }
                        target.VisualRoot.localScale = Vector3.one * _C_INITIAL_SCALE;

                        if (!getCommitted())
                        {
                            setCommitted(true);
                            Page.EmitMergeCommitted(target, isUpgrade, isClosure, completedGroup, fromTool);
                        }
                        Fx.SpawnMergeBurst(target.transform.position, target.GetRadius(), target.transform.parent);
                        SpawnMergeSpineEffect(target);

                        if (useNewClosurePop) yield break;
                    }
                    float t = (elapsed - F25) / (F35 - F25);
                    target.VisualRoot.localScale = Vector3.one * Mathf.Lerp(_C_INITIAL_SCALE, _C_KEYFRAME_35, t);
                }
                else if (!useNewClosurePop && elapsed <= F37)
                {
                    float t = (elapsed - F35) / (F37 - F35);
                    target.VisualRoot.localScale = Vector3.one * Mathf.Lerp(_C_KEYFRAME_35, _C_KEYFRAME_37, t);
                }
                else
                {
                    if (halo == null)
                    {
                        halo = new GameObject("MergeHalo");
                        halo.transform.SetParent(target.transform.parent, false);
                        halo.transform.position = target.transform.position;
                        haloSr = halo.AddComponent<SpriteRenderer>();
                        haloSr.sprite = AssetLib.Sprite("Art/Sprites/Bubble/disc");
                        haloSr.sortingOrder = 150;
                        haloBaseScale = (target.GetRadius() * 2f) / Mathf.Max(haloSr.sprite.rect.width, 1);
                        halo.transform.localScale = Vector3.one * haloBaseScale;
                    }
                    float segStart = useNewClosurePop ? F35 : F37;
                    float segFrom = useNewClosurePop ? _C_KEYFRAME_35 : _C_KEYFRAME_37;
                    float t = (elapsed - segStart) / (F42 - segStart);
                    target.VisualRoot.localScale = Vector3.one * Mathf.Lerp(segFrom, _C_KEYFRAME_42, t);
                    halo.transform.localScale = Vector3.one * (haloBaseScale * Mathf.Lerp(1f, _HALO_KEYFRAME_42, t));
                    var hc = haloSr.color; hc.a = Mathf.Lerp(1f, 0f, t); haloSr.color = hc;
                }
            }

            if (target != null) target.VisualRoot.localScale = Vector3.one;
            if (halo != null) Destroy(halo);
        }

        /// <summary>
        /// Control branch of merge_animation: a 0.45 second cubic convergence
        /// followed by the original 0.4 second sine pop. The recorded group-1
        /// branch above keeps its production F25/F35/F37/F42 keyframes.
        /// </summary>
        IEnumerator ControlAnimation(BubbleView src, BubbleView target,
            System.Action<bool> setCommitted, System.Func<bool> getCommitted,
            bool isUpgrade, bool isClosure, bool completedGroup, int myRound,
            List<PieceAnim> pieces, List<int> layoutQuads, float cellMerged,
            float fusionEnlarge, bool fromTool, bool useNewClosureFx)
        {
            float elapsed = 0f;
            float startR = target.Collider.radius;
            float finalR = target.GetRadius();
            bool useNewClosurePop = useNewClosureFx;
            float convergenceDuration = FUSION_DURATION *
                (isClosure ? CLOSURE_FUSION_MULT : 1f);

            src.BubbleSprite.gameObject.SetActive(false);
            target.BubbleSprite.gameObject.SetActive(false);

            while (elapsed < convergenceDuration)
            {
                yield return null;
                if (myRound < Page.RoundSeq || src == null || target == null)
                    yield break;

                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / convergenceDuration);
                float lt = 1f - Mathf.Pow(1f - raw, 3f);
                float dip = Yes2MergeDip(lt, isClosure);
                Vector3 targetPos = target.transform.position;

                var basePos = new Vector3[pieces.Count];
                for (int i = 0; i < pieces.Count; i++)
                {
                    Vector2 offset = TangramMatchContent.IsTangramImage(target.ImageId)
                        ? TangramMatchContent.FormationOffset(
                            target.ImageId,
                            pieces[i].Piece.Quadrant,
                            layoutQuads,
                            cellMerged,
                            target.Fragment.TangramMold) * fusionEnlarge
                        : CategoryMatchContent.IsCategoryImage(target.ImageId)
                        ? CategoryMatchContent.FormationOffset(
                            pieces[i].Piece.Quadrant,
                            layoutQuads,
                            cellMerged) * fusionEnlarge
                        : NumberMatchContent.IsNumberImage(target.ImageId) ||
                          WordMatchContent.IsWordImage(target.ImageId)
                        ? NumberMatchContent.FormationOffset(
                            pieces[i].Piece.Quadrant,
                            layoutQuads,
                            cellMerged) * fusionEnlarge
                        : BubbleView.QuadrantOffset(
                            pieces[i].Piece.Quadrant,
                            layoutQuads,
                            cellMerged) * fusionEnlarge;
                    Vector3 endPos = targetPos +
                        new Vector3(offset.x, -offset.y, 0f);
                    basePos[i] = Vector3.Lerp(
                        pieces[i].StartPos, endPos, lt);
                }

                Vector3 pivot = Vector3.zero;
                foreach (Vector3 pos in basePos) pivot += pos;
                if (basePos.Length > 0) pivot /= basePos.Length;

                for (int i = 0; i < pieces.Count; i++)
                {
                    PieceAnim piece = pieces[i];
                    if (piece.Piece.Go == null) continue;
                    float endScale = TangramMatchContent.IsTangramImage(target.ImageId)
                        ? cellMerged * fusionEnlarge
                        : CategoryMatchContent.IsCategoryImage(target.ImageId)
                        ? CategoryMatchContent.IconDisplaySize(
                            cellMerged,
                            layoutQuads.Count) /
                          piece.Piece.RegionWOrig * fusionEnlarge
                        : cellMerged / piece.Piece.RegionWOrig * fusionEnlarge;
                    float seamBoost = piece.IsIncoming
                        ? 1f + SEAM_OVERSCAN_EXP * Mathf.Sin(lt * Mathf.PI)
                        : 1f;
                    piece.Piece.Go.transform.position =
                        pivot + (basePos[i] - pivot) * dip;
                    piece.Piece.Go.transform.rotation = Quaternion.Euler(
                        0f, 0f, Mathf.LerpAngle(piece.StartRot, 0f, lt));
                    Vector3 scale = Vector3.Lerp(
                        piece.StartScale,
                        new Vector3(endScale, endScale, 1f),
                        lt) * seamBoost * dip;
                    SetWorldScale(piece.Piece.Go.transform, scale);
                }

                if (src.BubbleSprite != null)
                {
                    Color color = src.BubbleSprite.color;
                    color.a = 1f - lt;
                    src.BubbleSprite.color = color;
                }
                target.SetCollisionRadius(Mathf.Lerp(startR, finalR, lt));
            }

            if (!useNewClosurePop)
            {
                src.VisualRoot.gameObject.SetActive(false);
                target.BubbleSprite.gameObject.SetActive(true);
                Color bubbleColor = target.BubbleSprite.color;
                bubbleColor.a = 1f;
                target.BubbleSprite.color = bubbleColor;
                target.VisualRoot.gameObject.SetActive(true);
                target.VisualRoot.localScale = Vector3.one;
                target.Refresh();
                target.ResetImageRotation();
                target.ResetImageFloat();
            }

            if (!getCommitted())
            {
                setCommitted(true);
                Page.EmitMergeCommitted(
                    target, isUpgrade, isClosure, completedGroup, fromTool);
            }
            Fx.SpawnMergeBurst(
                target.transform.position, target.GetRadius(),
                target.transform.parent);
            SpawnMergeSpineEffect(target);

            if (useNewClosurePop) yield break;

            elapsed = 0f;
            const float popDuration = 0.4f;
            while (elapsed < popDuration)
            {
                yield return null;
                if (myRound < Page.RoundSeq || target == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float scale = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                target.VisualRoot.localScale = Vector3.one * scale;
            }
            if (target != null) target.VisualRoot.localScale = Vector3.one;
        }

        static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            var parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(
                parentScale.x != 0 ? worldScale.x / parentScale.x : worldScale.x,
                parentScale.y != 0 ? worldScale.y / parentScale.y : worldScale.y,
                1);
        }

        const float MERGE_EFFECT_NATIVE_SIZE = 410f;
        const float MERGE_EFFECT_SIZE_RATIO = 1.5f;

        void SpawnMergeSpineEffect(BubbleView target)
        {
            var go = new GameObject("MergeSpineFx");
            go.transform.SetParent(target.VisualRoot, false);
            var spine = go.AddComponent<SpineLite.SpineSprite>();
            spine.Load("bubble_special_effects");
            float diameter = target.GetRadius() * 2f;
            float effScale = diameter * MERGE_EFFECT_SIZE_RATIO / MERGE_EFFECT_NATIVE_SIZE;
            go.transform.localScale = new Vector3(effScale, effScale, 1);
            spine.SortingOrder = 40;
            var entry = spine.SetAnimation("animation", false);
            if (entry != null) entry.Completed = () => { if (go != null) Destroy(go); };
            else Destroy(go, 0.7f);
        }

        void RunClosureCollect(BubbleView target, int slotIdx)
        {
            var flyTex = target.SourceTexture;
            Vector3 startWorld = target.transform.position;
            float bubbleD = target.GetRadius() * 2f;
            int collectedImgId = target.ImageId;
            float startRot = 0f;

            bool isLast = slotIdx + 1 >= Page.TopBar.SlotCount;
            if (UseNewClosureEffects(isLast, false))
            {
                bool rs = UseRainbowShine(isLast, false);
                target.PlayClosureShowcase(rs);
                Page.Fly.Play(flyTex, startWorld, bubbleD, slotIdx, startRot, collectedImgId, true, rs);
            }
            else
            {
                target.Explode();
                Page.Fly.Play(flyTex, startWorld, bubbleD, slotIdx, startRot, collectedImgId, false, false);
            }
            Page.EmitImageCollected(collectedImgId, slotIdx, startWorld);
        }

        void MaybeApplyBlockSize(BubbleView target, int nFinal)
        {
            if (nFinal < 2 || nFinal > 3) return;
            int board = Page.CountBoardBubbleSlots();
            int peak = board;
            foreach (int size in Page.Scheduler.WaveSizes())
            {
                board += size;
                if (board > peak) peak = board;
                board = Mathf.Max(0, board - 4);
            }
            peak += Page.Scheduler.PendingGroupCount();
            float mult = 1.0f;
            if (peak <= 15) mult = 1.2f;
            else if (peak <= 18) mult = 1.15f;
            else if (peak <= 22) mult = 1.1f;
            float newR = Page.Field.BaseRadius * mult;
            if (newR > target.BaseRadius) target.BaseRadius = newR;
        }
    }
}
