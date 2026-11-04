using System.Text.RegularExpressions;

namespace DiscImageStudio.Localization;

/// <summary>
/// The burning layer reports its progress and errors as Chinese text. This turns those known
/// messages into the current language at the point they are shown. Each rule is a catalog key
/// whose <c>{n}</c> placeholders stand for the variable parts; the arguments are translated too
/// (an operation name, a disc type). Text that matches no rule, such as a message written by
/// Windows itself, is shown unchanged.
/// </summary>
public static class BurnMessageLocalizer
{
    private const int MaximumDepth = 3;

    private static readonly string[] Keys =
    [
        "正在检查刻录机和空白介质…",
        "正在准备盘片和写入设备…",
        "正在写入，等待刻录机进度…",
        "正在写入 CD 导入区…",
        "刻录完成，盘片会话已关闭。",
        "数据已全部写入，但盘片关闭步骤返回了错误 {0}。盘片内容通常可以读取，但可能没有封盘。",
        "当前盘片未报告可选速度，将使用刻录接口的保守回退设置…",
        "正在关闭 CD 刻录会话…",
        "所选光驱不支持这种刻录方式。",
        "请放入与所选 CD/DVD/蓝光模式兼容的可写盘片。",
        "当前盘片与所选 CD/DVD/蓝光模式不兼容。",
        "所选光驱不支持 CD DAO 所需的 Cooked R-W 子通道格式。",
        "蓝光刻录已取消。",
        "刻录流程已完成，但设备未能正常释放。",
        "设备释放期间还出现以下问题（已尝试后续清理）：",
        "初始化 Windows 刻录接口",
        "创建刻录格式",
        "连接刻录机",
        "检查刻录机支持",
        "检查当前盘片兼容性",
        "选择最低刻录速度",
        "写入 DVD 数据",
        "释放刻录格式 COM 对象",
        "释放刻录机 COM 对象",
        "锁定 BD-R 盘片",
        "检查未格式化 BD-R 的原始容量",
        "设置 BD-R 写入速度",
        "刷新 BD-R 缓存并封盘（请等待完成）",
        "准备并锁定 CD 介质",
        "检查 CD 是否为空白盘",
        "设置 CD DAO 扇区格式",
        "设置 CD 刻录速度",
        "使用 CD DAO 保守回退速度",
        "构造 CD DAO {0}",
        "写入 CD DAO {0}",
        "关闭 CD 刻录会话",
        "无备用区顺序写入 BD-R：{0} 扇区",
        "使用 CD DAO 末端 overburn（超出 {0} 个 DAO 扇区）",
        "数据轨",
        "音轨",
        "蓝光盘",
        "CD-R 或 CD-RW",
        "未格式化的单层顺序 BD-R（无备用区）",
        "正在向刻录接口传输 {0} / {1} MiB…",
        "刻录机已接收 {0} / {1} MiB…",
        "正在刻录 {0} / {1} MiB…",
        "自动选择最低写入速度：{0}×（{1} MB/s）…",
        "设备设置速度={0}× · {1} MB/s（设备返回的设置值；实时吞吐另行测量）。",
        "设备设置速度={0}× · {1} MB/s · CAV（设备返回的设置值；实时吞吐另行测量）。",
        "Windows 刻录接口在“{0}”时返回错误 {1}：{2}",
        "读取当前盘片的刻录速度失败（{0}）：{1}",
        "{0}失败（HRESULT {1}）：{2}",
        "{0} 剩余容量不足：需要 {1} 扇区（{2} MiB），实际可写 {3} 扇区（{4} MiB），超出 {5} 扇区（{6} MiB）。",
        "{0} 不是空白盘，已取消刻录。",
        "{0} 未报告可用的数据容量（{1} 扇区）。",
        "所选刻录模式需要可写的 {0} 盘片；当前盘片不兼容。",
        "CD DAO lead-out 超出盘片边界 {0} 个扇区；最多允许 {1} 个 DAO 扇区的受限 overburn。",
        "光驱 {0}",
        "无法创建 Windows 刻录组件 {0}。",
        "Windows Image Mastering API 2 (IMAPI2) 不可用。",
        "要求的功能页面受支持，但现在没有标记为当前页面"
    ];

    private static readonly (Regex Pattern, string Key)[] Rules = Keys
        .Select(key => (BuildPattern(key), key))
        .ToArray();

    public static string Localize(string? message) => Localize(message, 0);

    private static string Localize(string? message, int depth)
    {
        if (string.IsNullOrEmpty(message) || depth >= MaximumDepth)
        {
            return message ?? string.Empty;
        }

        string[] lines = message.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            lines[index] = LocalizeLine(lines[index], depth);
        }

        return string.Join("\n", lines);
    }

    private static string LocalizeLine(string line, int depth)
    {
        string trimmed = line.TrimEnd('\r', ' ');
        if (trimmed.Length == 0)
        {
            return line;
        }

        foreach ((Regex pattern, string key) in Rules)
        {
            Match match = pattern.Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            object?[] arguments = match.Groups.Cast<Group>().Skip(1)
                .Select(group => (object?)Localize(group.Value.Trim(), depth + 1))
                .ToArray();
            return L.Format(key, arguments);
        }

        return line;
    }

    private static Regex BuildPattern(string key)
    {
        string pattern = "^" + Regex.Replace(
            Regex.Escape(key).Replace(@"\{", "{"),
            @"\{\d+\}",
            "(.+?)") + "$";
        return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}
