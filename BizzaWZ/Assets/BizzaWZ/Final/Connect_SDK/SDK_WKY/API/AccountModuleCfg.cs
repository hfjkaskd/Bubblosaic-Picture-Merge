using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Obfuz.ObfuzIgnore]
public partial class AccountModuleCfg
{
    public const string add_ecpm = "/Api/Match/AddRe"; // 添加收益  可以点击查看 ---------------------------------
    public const string add_order = "/Api/Match/Pay"; // 去提现 - 全部提现 可以点击查看   ---------------------------------
    public const string add_order_task = "/Api/Match/TaskPay"; // 去提现 - 现金或奖卷 可以点击查看  ---------------------------------
    public const string base_list = "/Api/Match/AppConfig"; // 其它配置信息 - 可以点击查看  ---------------------------------
    public const string from = "/Api/Match/Come"; // 用户归因 - 后台查看   --------------------------------- 这个 AI写的不太对 VN
    public const string get_ecpm_id = "/Api/Match/ReId"; // 获取收益ID - 可以点击查看 --------------------------------- 
    public const string login = "/Api/Match/Login"; // 用户登陆 - 可以点击查看 --------------------------------- 
    public const string msg = "/Api/Match/Notice"; // 添加反馈 - 需要服务器查看 ---------------------------------
    public const string msg_list = "/Api/Match/NoticeList"; // 反馈列表 - 需要服务器查看  ---------------------------------
    public const string new_user = "/Api/Match/New"; // 新用户奖励-领取 - 可以点击查看 ---------------------------------
    public const string number = "/Api/Match/Level"; // 关卡，完成次数上报 - 可以点击查看 ---------------------------------
    public const string order_list = "/Api/Match/Order"; // 订单列表 - 可以点击查看 ---------------------------------
    public const string order_name = "/Sport/Balls/OrderName"; // 提现用户列表 - 可以点击查看 
    public const string plat_from = "/Api/Match/Page"; // 提现平台 - 可以点击查看 ---------------------------------
    public const string task_list = "/Api/Match/Task"; // 每日任务-现金 - 可以点击查看
    public const string user_info = "/Api/Match/Info"; // 用户信息 - 可以点击查看 ---------------------------------
    public const string user_name = "/Api/Match/Name"; // 昵称添加
    public const string ad_info = "/Api/MatchLog/AdShow"; // 广告上报 - 可以点击查看
    public const string app_info = "/Api/MatchLog/AppEven"; // 日志上报 - 可以点击查看


    #region 每日任务
    public const string one_Count = "hp_one";
    public const string one_Ratio = "hp_one_rate";
    public const string two_Count = "hp_two";
    public const string two_Ratio = "hp_two_rate";
    public const string three_Count = "hp_three";
    public const string three_Ratio = "hp_three_rate";
    #endregion
}

/*
public const string add_ecpm = ""; // 添加收益  可以点击查看 ---------------------------------
    public const string add_order = ""; // 去提现 - 全部提现 可以点击查看   ---------------------------------
    public const string add_order_task = ""; // 去提现 - 现金或奖卷 可以点击查看  ---------------------------------
    public const string base_list = ""; // 其它配置信息 - 可以点击查看  ---------------------------------
    public const string from = ""; // 用户归因 - 后台查看   --------------------------------- 这个 AI写的不太对 VN
    public const string get_ecpm_id = ""; // 获取收益ID - 可以点击查看 --------------------------------- 
    public const string login = ""; // 用户登陆 - 可以点击查看 --------------------------------- 
    public const string msg = ""; // 添加反馈 - 需要服务器查看 ---------------------------------
    public const string msg_list = ""; // 反馈列表 - 需要服务器查看  ---------------------------------
    public const string new_user = ""; // 新用户奖励-领取 - 可以点击查看 ---------------------------------
    public const string number = ""; // 关卡，完成次数上报 - 可以点击查看 ---------------------------------
    public const string order_list = ""; // 订单列表 - 可以点击查看 ---------------------------------
    public const string order_name = ""; // 提现用户列表 - 可以点击查看 
    public const string plat_from = ""; // 提现平台 - 可以点击查看 ---------------------------------
    public const string task_list = ""; // 每日任务-现金 - 可以点击查看
    public const string user_info = ""; // 用户信息 - 可以点击查看 ---------------------------------
    public const string user_name = ""; // 昵称添加
    public const string ad_info = ""; // 广告上报 - 可以点击查看
    public const string app_info = ""; // 日志上报 - 可以点击查看
*/