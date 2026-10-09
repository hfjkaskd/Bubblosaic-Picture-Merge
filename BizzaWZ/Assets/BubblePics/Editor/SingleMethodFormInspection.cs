using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewSingleMethodCore(string folder, Action<bool, string> check)
        {
            var saved = SaveDataUtils.GameData;
            string savedCpf = saved.withdrawCPFInfo, savedName = saved.withdrawNameInfo, savedMail = saved.withdrawEmailInfo;
            var pag = new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform
                { Os_Cn = UIWithdrawalPanel.pagBankInfo, Os_Me = "PagBank", Os_Mlt = .01 };
            var pix = new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform
                { Os_Cn = UIWithdrawalPanel.pixInfo, Os_Me = "PIX", Os_Mlt = .01 };
            try
            {
                LanguageUtils.SelectedLanguage = "pt-BR";
                Localization.SetLocale("pt_BR");
                var page = (UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel, pag,
                    new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform> { pag },
                    E_WithdrawType.Fake, (Action)null, false);
                inspectedPage = page;
                await UniTask.Delay(500, ignoreTimeScale: true);
                CheckFixedIcon(page, "PagBankLogo", check);
                page.CPFNumberInput.Text = page.accountNameInput.Text = page.paypalMailInput.Text = "";
                await UniTask.DelayFrame(4);
                await PayPalCapture(folder, "PagBank-Single-Unity.png");
                var action = page.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>();
                var close = page.transform.Find("Root/FillRoot/pageClose").GetComponent<Button>();
                check(HitStandard(action) && HitStandard(close), "Single PagBank Continue and Close receive native Button input");
                CheckContinuePresentation(action, check);
                check(!InputVerifyUtil.IsValidCPF("93575567487"), "Reported completed-form CPF is rejected by the existing checksum");
                check(page.IsValidName("sdafd"), "Reported completed-form name passes existing validation");
                foreach (var field in new[] { page.CPFNumberInput, page.accountNameInput, page.paypalMailInput })
                {
                    check(field.gameObject.activeInHierarchy, "Required input remains visible: " + field.name);
                    field.ManualSelect();
                    await UniTask.DelayFrame(2);
                    check(field.Selected, "Native input can focus: " + field.name);
                    field.ManualDeselect();
                    page.client.HideKeyboard();
                }
                check(InputVerifyUtil.IsValidCPF("81721848320"), "Reported CPF passes the existing checksum algorithm");
                page.CPFNumberInput.Text = "81721848321";
                page.accountNameInput.Text = "Test User";
                page.paypalMailInput.Text = "invalid";
                PressStandard(action);
                await UniTask.DelayFrame(4);
                page.client.HideKeyboard();
                check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel)
                    && page.CPFNumberErrorTra.gameObject.activeSelf && page.paypalMailErrorTra.gameObject.activeSelf,
                    "Invalid PagBank identity/email cannot advance and show inline errors");
                await PayPalCapture(folder, "CPF-Invalid-Unity.png");
                page.CPFNumberInput.SetText("8172184832", invokeTextChangeEvent: true);
                await UniTask.DelayFrame(3);
                check(page.CPFNumberErrorTra.gameObject.activeSelf, "Incomplete correction retains the CPF error");
                page.CPFNumberInput.SetText("81721848320", invokeTextChangeEvent: true);
                await UniTask.DelayFrame(3);
                check(!page.CPFNumberErrorTra.gameObject.activeSelf, "Correcting to the reported CPF clears the error without pressing Continue");
                check(page.paypalMailErrorTra.gameObject.activeSelf, "Correcting CPF does not hide unrelated email validation");
                page.CPFNumberInput.Text = "817.218.483-21";
                check(!page.IsValidCpfOrCnpj(page.CPFNumberInput.Text), "Formatted CPF with incorrect checksum is rejected");
                page.CPFNumberInput.SetText("817.218.483-20", invokeTextChangeEvent: true);
                await UniTask.DelayFrame(3);
                check(!page.CPFNumberErrorTra.gameObject.activeSelf, "Formatted valid CPF clears the previous error on edit");
                page.CPFNumberInput.Text = "81721848320";
                page.accountNameInput.Text = "Test User";
                page.paypalMailInput.Text = "player@example.com";
                // Submit once with an invalid name to verify CPF stays clear while
                // another field is rejected, without navigating away from the form.
                page.accountNameInput.Text = "";
                PressStandard(action);
                await UniTask.DelayFrame(4);
                page.client.HideKeyboard();
                check(!page.CPFNumberErrorTra.gameObject.activeSelf && page.accountNameErrorTra.gameObject.activeSelf,
                    "Valid reported CPF remains clear when another field fails submission");
                page.accountNameInput.Text = "Test User";
                page.IsValidName(page.accountNameInput.Text);
                // Let the earlier rejected submission's toast and focus transition finish.
                await UniTask.Delay(3000, ignoreTimeScale: true);
                foreach (var field in new[] { page.CPFNumberInput, page.accountNameInput, page.paypalMailInput })
                    field.ManualDeselect();
                page.client.HideKeyboard();
                await UniTask.Delay(300, ignoreTimeScale: true);
                // Reproduce the reported CPF with test name/email and retain the actual validators.
                page.CPFNumberInput.SetText("93575567487", invokeTextChangeEvent: true);
                page.accountNameInput.SetText("Test User", invokeTextChangeEvent: true);
                page.paypalMailInput.SetText("player@example.com", invokeTextChangeEvent: true);
                PressStandard(action);
                await UniTask.DelayFrame(4);
                check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel)
                    && page.CPFNumberErrorTra.gameObject.activeSelf
                    && !page.paypalMailErrorTra.gameObject.activeSelf,
                    "Reported completed form identifies the invalid CPF without rejecting the email");
                await UniTask.Delay(3000, ignoreTimeScale: true);
                foreach (var field in new[] { page.CPFNumberInput, page.accountNameInput, page.paypalMailInput })
                    field.ManualDeselect();
                page.client.HideKeyboard();
                await UniTask.Delay(800, ignoreTimeScale: true);
                await PayPalCapture(folder, "PagBank-Reported-CPF-Error-Unity.png");
                // Return to the existing valid local fixture; never submit a payout.
                page.CPFNumberInput.SetText("81721848320", invokeTextChangeEvent: true);
                await UniTask.DelayFrame(3);
                check(!page.CPFNumberErrorTra.gameObject.activeSelf, "Valid test CPF clears the reported-form error");
                CheckContinuePresentation(action, check);
                await PayPalCapture(folder, "CPF-Corrected-Unity.png");
                page.paypalMailInput.ManualSelect();
                await UniTask.Delay(700, ignoreTimeScale: true);
                check(action.IsInteractable() && HitStandard(action), "Completed form Continue stays enabled and reachable with the email keyboard open");
                await PayPalCapture(folder, "PagBank-Complete-Keyboard-Unity.png");
                PressStandard(action);
                for (int i = 0; i < 20 && !UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel); i++)
                    await UniTask.Delay(100, ignoreTimeScale: true);
                page.client.HideKeyboard();
                var confirmation = UIModule.Instance.GetPage<UIWithdrawalConfirmPanel>();
                check(confirmation != null, "Valid PagBank form opens original confirmation; no payout submitted");
                await UniTask.Delay(800, ignoreTimeScale: true);
                await PayPalCapture(folder, "PagBank-Validated-Unity.png");
                if (confirmation != null) UIModule.Instance.ClosePage(confirmation);
                PressStandard(close);
                await UniTask.DelayFrame(4);
                check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalPanel), "Native Close dismisses account form");
                inspectedPage = null;

                page = (UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel, pix,
                    new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform> { pix },
                    E_WithdrawType.Fake, (Action)null, false);
                inspectedPage = page;
                await UniTask.DelayFrame(5);
                CheckFixedIcon(page, "PixLogo", check);
                CheckContinuePresentation(page.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>(), check);
                await PayPalCapture(folder, "PIX-Single-Unity.png");
                CloseRuntime();
                await UniTask.DelayFrame(3);

                page = (UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel, pag,
                    new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform> { pix, pag },
                    E_WithdrawType.Fake, (Action)null, true);
                inspectedPage = page;
                await UniTask.DelayFrame(6);
                var ways = page.PlatformRoot.GetComponentsInChildren<WithdrawWay>();
                check(ways.Length == 2 && !page.PlatformIconRoot.activeSelf, "Two methods remain directly visible without a duplicate fixed logo");
                foreach (var way in ways)
                    check(HitStandard(way.GetComponent<Button>()), "Visible method owns reachable native Button: " + way.data.Os_Cn);
                await PayPalCapture(folder, "PagBank-TwoMethods-Unity.png");
                foreach (var way in ways)
                    if (way.data.Os_Cn == UIWithdrawalPanel.pixInfo) { PressStandard(way.GetComponent<Button>()); break; }
                await UniTask.DelayFrame(5);
                check(page.accountIdentificationInput.gameObject.activeInHierarchy, "Switch to PIX restores key inputs");
                foreach (var way in page.PlatformRoot.GetComponentsInChildren<WithdrawWay>())
                    if (way.data.Os_Cn == UIWithdrawalPanel.pagBankInfo) { PressStandard(way.GetComponent<Button>()); break; }
                await UniTask.DelayFrame(5);
                check(page.paypalMailInput.gameObject.activeInHierarchy, "Switch back to PagBank restores email input");
            }
            finally
            {
                CloseRuntime();
                saved.withdrawCPFInfo = savedCpf;
                saved.withdrawNameInfo = savedName;
                saved.withdrawEmailInfo = savedMail;
                SaveDataUtils.gameStrategy.SaveData();
            }
        }

        static void CheckContinuePresentation(Button button, Action<bool, string> check)
        {
            var image = button.GetComponent<Image>();
            check(button.targetGraphic == image && button.IsInteractable(), "Enabled Continue uses its own native Button graphic");
            check(ReleaseArtInspection.BakedSprite(image, "Continue") && image.material.shader.name == "UI/Default",
                "Enabled Continue uses the baked green action appearance");
            foreach (var caption in button.GetComponentsInChildren<ApprovedHudCaption>(true))
                check(!caption.isActiveAndEnabled, "Old full-button caption cannot cover the enabled action");
            var label = button.transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>();
            var group = label.GetComponent<CanvasGroup>();
            check(label.gameObject.activeInHierarchy && (group == null || group.alpha == 1), "Live translated button label is visible");
        }

        static void CheckFixedIcon(UIWithdrawalPanel page, string sprite, Action<bool, string> check)
        {
            var icon = page.paymentImage;
            check(page.PlatformIconRoot.activeSelf && !page.PlatformRoot.activeSelf, "Only the fixed payment presentation is visible: " + sprite);
            check(ReleaseArtInspection.NamedSprite(icon.sprite, sprite), "Fixed form uses approved resource icon: " + sprite);
            check(ReleaseArtInspection.BakedSprite(icon, sprite) && icon.material.shader.name == "UI/Default",
                "Fixed icon uses the authored transparent matte: " + sprite);
            var card = PayPalRect((RectTransform)page.PlatformIconRoot.transform);
            var bounds = PayPalRect(icon.rectTransform);
            check(card.Contains(bounds.min) && card.Contains(bounds.max), "Fixed logo stays within payment card: " + sprite);
            check(Vector2.Distance(card.center, bounds.center) < 1 && bounds.height <= 104,
                "Fixed logo is centered with vertical padding: " + sprite);
        }
    }
}
