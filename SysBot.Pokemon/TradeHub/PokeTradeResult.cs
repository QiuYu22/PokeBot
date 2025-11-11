namespace SysBot.Pokemon;

public enum PokeTradeResult
{
    Success,

    // Trade Partner Failures
    NoTrainerFound,

    TrainerTooSlow,

    TrainerLeft,

    TrainerOfferCanceledQuick,

    TrainerRequestBad,

    IllegalTrade,

    SuspiciousActivity,

    UserCanceled,

    // Recovery -- General Bot Failures
    // Anything below here should be retried once if possible.
    RoutineCancel,

    ExceptionConnection,

    ExceptionInternal,

    RecoverStart,

    RecoverPostLinkCode,

    RecoverOpenBox,

    RecoverReturnOverworld,

    RecoverEnterUnionRoom,
}

public static class PokeTradeResultExtensions
{
    public static bool ShouldAttemptRetry(this PokeTradeResult t) => t >= PokeTradeResult.RoutineCancel;

    public static string ToLocalizedString(this PokeTradeResult result) => result switch
    {
        PokeTradeResult.Success => "交易成功",
        PokeTradeResult.NoTrainerFound => "未找到训练师",
        PokeTradeResult.TrainerTooSlow => "训练师响应过慢",
        PokeTradeResult.TrainerLeft => "训练师已离开",
        PokeTradeResult.TrainerOfferCanceledQuick => "训练师过快取消提供",
        PokeTradeResult.TrainerRequestBad => "训练请求无效",
        PokeTradeResult.IllegalTrade => "检测到非法交易",
        PokeTradeResult.SuspiciousActivity => "检测到可疑行为",
        PokeTradeResult.UserCanceled => "用户取消",
        PokeTradeResult.RoutineCancel => "流程被中止",
        PokeTradeResult.ExceptionConnection => "连接异常",
        PokeTradeResult.ExceptionInternal => "内部异常",
        PokeTradeResult.RecoverStart => "恢复流程：初始化阶段",
        PokeTradeResult.RecoverPostLinkCode => "恢复流程：发送密码后",
        PokeTradeResult.RecoverOpenBox => "恢复流程：打开盒子时",
        PokeTradeResult.RecoverReturnOverworld => "恢复流程：返回主界面时",
        PokeTradeResult.RecoverEnterUnionRoom => "恢复流程：进入联机房间时",
        _ => result.ToString(),
    };
}
