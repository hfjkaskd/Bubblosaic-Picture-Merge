using Bizza.Sdk;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class WKY_Flow
{
    public bool WKYFlowOver = false;
    public static bool isInit = false;
    private static bool _initializationStarted = false;
    public static bool IsInitializing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        isInit = false;
        _initializationStarted = false;
        IsInitializing = false;
    }

    public async UniTask Start_Init_Flow()
    {
        if (_initializationStarted)
        {
            string message = isInit
                ? "WKY SDK 已经初始化完成，不允许重复初始化。"
                : "WKY SDK 已经开始初始化，不允许重复或并发初始化。";
            Debug.LogError(message);
            throw new System.InvalidOperationException(message);
        }

        _initializationStarted = true;
        IsInitializing = true;
        WKYFlowOver = false;
        isInit = false;

        try
        {
            await Init();
            if (!WKYFlowOver)
            {
                throw new System.InvalidOperationException(
                    "WKY SDK 初始化失败，不能继续进入游戏。");
            }

            isInit = true;
        }
        catch
        {
            WKYFlowOver = false;
            throw;
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private async UniTask Init()
    {
        WKYFlowOver = false;
Bizza.Unity.Android.AndroidJavaMessageDispatcher.SetHttpMessageHandler(
    HttpUtil.LoginHTTPDict);
        ChannelConfig config = await ChannelConfigLoader.LoadAsync();
        if (config == null)
        {
            Debug.LogError(
                "ChannelConfig 加载失败，SDK 初始化终止。");
            return;
        }
        LogLogger.LogInfo("State_ChannelConfig: ", $"<color=red>----------------------------</color>");
#if DEBUG_MODE
        LogLogger.LogAdInfo($"{config.GetAllParamsString()}");
#endif

        var customDeviceInfo = new DeviceInfoUtil.CustomDeviceInfo
        {
            appId = config.AppId,
            country = config.real_CustomConfig.CountryName,
            defaultCountry = config.real_CustomConfig.DefaultCountryName,
            fakeAndroidId = config.real_CustomConfig.GenerateFakeAndroidId
        };
        await DeviceInfoUtil.InitializeAsync(customDeviceInfo);
        var deciceInfo = DeviceInfoUtil.Data;
        LogLogger.LogInfo("State_deviceInfo: ", $"<color=red>----------------------------</color>");
#if DEBUG_MODE
        {
            string _info = JsonUtility.ToJson(deciceInfo);
            Debug.Log(_info);
        }
#endif

        LogLogger.LogInfo("State_analyticsAgent: ", $"<color=red>----------------------------</color>");

        AccountModule account = new AccountModule();
        account.Login();
#if BIZZA_HTTP_AD
        await UniTask.WaitUntil(() =>
            account.LoadingFinish ||
            account.LoadingUserDataFailed);

        if (account.LoadingUserDataFailed || account.Os_Current_Uso == null)
        {
            Debug.LogError(
                "账号数据加载失败，无法继续初始化 SDK。"
                + account.LoadingUserDataErrorCode);

            return;
        }
#else
        await UniTask.WaitUntil(() =>
                            account.LoadingFinish);
#endif

        LogLogger.LogInfo("State_deviceInfo2: ", $"<color=red>----------------------------</color>");
#if DEBUG_MODE
        {
            string _info = JsonUtility.ToJson(deciceInfo);
            Debug.Log(_info);
        }
#endif
        LogLogger.LogInfo("State_AccountInfo: ", $"{account.Os_Current_Uso?.GetInfo()}");

        var platform = new OverseaPlatform(config);

        WKYFlowOver = true;
    }
}
