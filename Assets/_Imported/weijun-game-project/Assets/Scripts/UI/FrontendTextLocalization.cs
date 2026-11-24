using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class FrontendTextLocalization
{
    private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "地块配置不能为空！", "暂时无法选择这块地，请重新选择。" },
        { "玩家 ID", "玩家编号" },
        { "玩家ID", "玩家编号" },
        { "Player ID", "玩家编号" },
        { "PlayerID", "玩家编号" },
        { "Lv.", "等级 " },
        { "FPS", "帧率" },
        { "The backend has not unlocked placement on this land slot.", "此地块尚未解锁。" },
        { "The backend currently rejects this land configuration.", "当前无法放置这块地。" },
        { "Land config is missing.", "缺少地块配置。" },
        { "No land slot under pointer.", "指针下没有地块。" },
        { "Current land slot is already occupied.", "当前地块已被占用。" },
        { "Target land is not placed.", "目标地块尚未购买。" },
        { "Target land already has a building.", "目标地块已有建筑。" },
        { "Target land does not allow this building type.", "目标地块不允许建造此类型的建筑。" },
        { "MapManager is missing.", "地图管理器缺失。" },
        { "BuildManager is missing from the scene.", "场景中缺少建造管理器。" },
        { "LandConfig or LandPreview prefab is not assigned.", "未配置地块或预览预制体。" },
        { "LandPreview prefab must contain a LandPreview component.", "地块预览预制体必须包含地块预览组件。" },
        { "Backend session or scene id is missing.", "服务器会话或场景编号缺失。" },
        { "A land scene switch is already pending.", "地块场景切换正在处理中。" },
        { "A building sync is already pending.", "正在处理建筑操作，请稍候。" },
        { "A valid idempotency key is required.", "需要有效的幂等键。" },
        { "Already logged out.", "已经退出登录。" },
        { "Idle Adventure reference data loaded.", "闯关模式参考数据已加载。" },
        { "was loading.", "正在读取。" },
        { "was refreshing.", "正在刷新。" },
        { "was reconciling.", "正在同步。" },
        { "was superseded by a newer request.", "已被更新的请求替代。" },
        { "was superseded before reconciliation completed.", "在同步完成前已被更新请求替代。" },
        { "was superseded while rewards were refreshing.", "刷新奖励期间已被更新请求替代。" },
        { "was superseded while resources were refreshing.", "刷新资源期间已被更新请求替代。" },
        { "was superseded while state was refreshing.", "刷新状态期间已被更新请求替代。" },
        { "Backend action", "服务器行动" },
        { "Backend identifier", "服务器编号" },
        { "Backend available", "服务器可用" },
        { "Backend building", "服务器建筑" },
        { "Backend collect-all", "服务器一键收取" },
        { "Backend inventory", "服务器背包" },
        { "Backend land", "服务器地块" },
        { "Backend login", "服务器登录" },
        { "Backend logout", "服务器退出登录" },
        { "Backend map", "服务器地图" },
        { "Backend NPC", "服务器居民" },
        { "Backend player", "服务器玩家" },
        { "Backend production", "服务器生产" },
        { "Backend register", "服务器注册" },
        { "Backend resolved", "服务器已解析" },
        { "Backend returned", "服务器返回" },
        { "Backend scene", "服务器场景" },
        { "Backend session", "服务器会话" },
        { "Backend round", "服务器回合" },
        { "The backend", "服务器" },
        { "Current", "当前" },
        { "Target", "目标" },
        { "Production", "生产" },
        { "Resident", "居民" },
        { "MANUAL_REVIEW", "人工审核" },
        { "FAILURE", "失败" },
        { "FAILED", "失败" },
        { "SUCCESS", "成功" },
        { "RUNNING", "进行中" },
        { "PAID", "已支付" },
        { "PAYING", "支付中" },
        { "CREATED", "已创建" },
        { "REJECT", "拒绝" },
        { "PONG", "连接正常" },
        { "MONTHLY", "每月" },
        { "WEEKLY", "每周" },
        { "DAILY", "每日" },
        { "LIFETIME", "长期" },
        { "FEATURED", "精选" },
        { "GROWTH", "成长" },
        { "ACTIVITY", "活动" },
        { "MAIN", "主要" },
        { "SINGLE", "单抽" },
        { "TEN", "十连抽" },
        { "FIVE", "五连抽" },
        { "AD_REWARD_CLAIMED", "广告奖励已领取" },
        { "OPERATION_NOTICE", "运营提醒" },
        { "MAIL_RECEIVED", "新邮件" },
        { "MAIL_ATTACHMENT_CLAIMED", "邮件附件已领取" },
        { "TICKET", "道具" },
        { "WEAPON", "武器" },
        { "ARMOR", "护甲" },
        { "ACCESSORY", "饰品" },
        { "CHEST", "宝箱" },
        { "STAMINA", "体力" },
        { "EXP", "经验" },
        { "XP", "经验" },
        { "VIP", "贵宾" },
        { "SOUL_CRYSTAL", "灵魂晶石" },
        { "SoulCrystal", "灵魂晶石" },
        { "Farm", "农场" },
        { "Mine", "矿场" },
        { "Lumbermill", "伐木场" },
        { "Workshop", "工坊" },
        { "House", "住宅" },
        { "Adventure Token", "闯关代币" },
        { "Idle Adventure mode", "闯关模式" },
        { "Idle Adventure", "闯关" },
        { "Power Strike", "强力一击" },
        { "Battle Focus", "战斗专注" },
        { "Piercing Hit", "穿刺打击" },
        { "Guard Break", "破防" },
        { "Heavy Blow", "重击" },
        { "Quick Tempo", "快速节奏" },
        { "Intimidate", "威慑" },
        { "Damage Up", "伤害提升" },
        { "Fire Damage Down", "火焰伤害降低" },
        { "Fire Field Up", "火焰领域增强" },
        { "Claw", "利爪" },
        { "Rend", "撕裂" },
        { "normal_damage_up", "伤害提升" },
        { "fire_damage_down", "火焰伤害降低" },
        { "fire_field_up", "火焰领域增强" },
        { "attribute_damage", "属性伤害" },
        { "adventure_token", "闯关代币" },
        { "PHYSICAL", "物理" },
        { "FIRE", "火" },
        { "WATER", "水" },
        { "WOOD", "木材" },
        { "EARTH", "土" },
        { "LIGHT", "光" },
        { "DARK", "暗" },
        { "Physical", "物理" },
        { "Fire", "火" },
        { "Water", "水" },
        { "Wood", "木材" },
        { "Earth", "土" },
        { "Light", "光" },
        { "Dark", "暗" },
        { "Backend session is not authenticated.", "服务器会话未认证。" },
        { "No backend response.", "未收到服务器响应。" },
        { "Backend returned an empty response.", "服务器返回空响应。" },
        { "Failed to parse backend response: ", "服务器响应解析失败：" },
        { "Request failed", "请求失败" },
        { "Network error", "网络错误" },
        { "Connection error", "连接错误" },
        { "ConnectionError", "连接错误" },
        { "ProtocolError", "协议错误" },
        { "DataProcessingError", "数据处理错误" },
        { "HttpError", "网络请求错误" },
        { "InvalidData", "数据无效" },
        { "Backend API", "服务器接口" },
        { "Backend", "服务器" },
        { "PAYMENT_DELIVERED", "支付到账" },
        { "RESOURCE_READY", "资源可收取" },
        { "BUILDING_COMPLETED", "建筑完成" },
        { "UNAUTHENTICATED", "未认证" },
        { "AUTHENTICATED", "已认证" },
        { "UNKNOWN", "未知" },
        { "INACTIVE", "未启用" },
        { "ACTIVE", "正常" },
        { "CLOSED", "已关闭" },
        { "PENDING", "待处理" },
        { "PROCESSING", "处理中" },
        { "CANCELLED", "已撤销" },
        { "COMPLETED", "已完成" },
        { "DELIVERED", "已送达" },
        { "REFUNDED", "已退款" },
        { "APPROVED", "已批准" },
        { "REJECTED", "已拒绝" },
        { "EXPIRED", "已过期" },
        { "IN_PROGRESS", "处理中" },
        { "INPROGRESS", "处理中" },
        { "ONLINE", "在线" },
        { "OFFLINE", "离线" },
        { "EQUIPPED", "已装备" },
        { "UNEQUIPPED", "未装备" },
        { "UPGRADING", "升级中" },
        { "BUILDING", "建造中" },
        { "DEFEATED", "已战败" },
        { "VICTORY", "胜利" },
        { "DRAW", "平局" },
        { "TIMEOUT", "已超时" },
        { "INITIALIZING", "初始化中" },
        { "DISCONNECTED", "已断开" },
        { "CONNECTED", "已连接" },
        { "UNAVAILABLE", "不可用" },
        { "AVAILABLE", "可用" },
        { "LOCKED", "已锁定" },
        { "Unknown", "未知" },
        { "Inactive", "未启用" },
        { "Active", "正常" },
        { "Closed", "已关闭" },
        { "Pending", "待处理" },
        { "Processing", "处理中" },
        { "Cancelled", "已撤销" },
        { "Completed", "已完成" },
        { "Unavailable", "不可用" },
        { "Available", "可用" },
        { "Locked", "已锁定" },
        { "Delivered", "已送达" },
        { "Refunded", "已退款" },
        { "Approved", "已批准" },
        { "Rejected", "已拒绝" },
        { "Expired", "已过期" },
        { "InProgress", "处理中" },
        { "Online", "在线" },
        { "Offline", "离线" },
        { "Equipped", "已装备" },
        { "Unequipped", "未装备" },
        { "Upgrading", "升级中" },
        { "Defeated", "已战败" },
        { "Victory", "胜利" },
        { "Draw", "平局" },
        { "Timeout", "已超时" },
        { "Initializing", "初始化中" },
        { "Disconnected", "已断开" },
        { "Connected", "已连接" },
        { "In progress", "处理中" },
        { "EQUIPMENT", "装备" },
        { "NPC", "居民" },
        { "HTTP", "网络请求" },
        { "code=", "错误码 " },
        { "result=", "结果：" },
        { "body=", "响应：" },
        { "Button", "按钮" },
        { "New Text", "提示" },
        { "GOLD", "金币" },
        { "STONE", "石材" },
        { "IRON", "铁矿" },
        { "Gold", "金币" },
        { "Stone", "石材" },
        { "Iron", "铁矿" },
        { "Crystal", "钻石" },
        { "gold", "金币" },
        { "wood", "木材" },
        { "stone", "石材" },
        { "iron", "铁矿" },
        { "crystal", "钻石" },
        { "COMMON", "普通" },
        { "RARE", "稀有" },
        { "EPIC", "史诗" },
        { "LEGENDARY", "传说" },
        { "Common", "普通" },
        { "Rare", "稀有" },
        { "Epic", "史诗" },
        { "Legendary", "传说" },
        { "Hero", "我方角色" },
        { "Enemy", "敌人" },
        { "Friendly", "我方" },
        { "Equipment", "装备" },
        { "Item", "物品" },
        { "Building", "建筑" },
        { "Land", "地块" },
        { "Stage", "关卡" },
        { "Round", "回合" },
        { "Battle", "战斗" },
        { "Attack", "攻击" },
        { "Defense", "防御" },
        { "Health", "生命" },
        { "Speed", "速度" },
        { "Power", "战力" },
        { "Damage", "伤害" },
        { "Cooldown", "冷却" },
        { "Turn", "回合" },
        { "Source", "来源" },
        { "Benefit", "增益" },
        { "Detriment", "减益" },
        { "Normal", "普通" },
        { "Attribute", "属性" },
        { "Field", "场地" },
        { "Skill", "技能" },
        { "Player", "玩家" },
        { "Character", "角色" },
        { "Role", "身份" },
        { "Formation", "编队" },
        { "Combat", "战斗" },
        { "System", "系统" },
        { "Mail", "邮件" },
        { "Task", "任务" },
        { "Shop", "商店" },
        { "Inventory", "背包" },
        { "Settings", "设置" },
        { "Profile", "资料" },
        { "Login", "登录" },
        { "Register", "注册" },
        { "Logout", "退出登录" },
        { "Start", "开始" },
        { "Continue", "继续" },
        { "Pause", "暂停" },
        { "Exit", "退出" },
        { "Next", "下一步" },
        { "Previous", "上一步" },
        { "Back", "返回" },
        { "Home", "首页" },
        { "Map", "地图" },
        { "Area", "区域" },
        { "Account", "账号" },
        { "Username", "账号" },
        { "Password", "密码" },
        { "Nickname", "昵称" },
        { "Level", "等级" },
        { "Reward", "奖励" },
        { "Price", "价格" },
        { "Quantity", "数量" },
        { "Amount", "数量" },
        { "Count", "数量" },
        { "Status", "状态" },
        { "Description", "描述" },
        { "Details", "详情" },
        { "History", "记录" },
        { "Order", "订单" },
        { "Purchase", "购买" },
        { "Build", "建造" },
        { "Upgrade", "升级" },
        { "Demolish", "拆除" },
        { "Move", "移动" },
        { "Assign", "分配" },
        { "Remove", "移除" },
        { "Collect", "收取" },
        { "Use", "使用" },
        { "Equip", "装备" },
        { "Unequip", "卸下" },
        { "Buy", "购买" },
        { "Sell", "出售" },
        { "Search", "搜索" },
        { "Refund", "退款" },
        { "Privacy", "隐私" },
        { "Policy", "协议" },
        { "Agreement", "协议" },
        { "Network", "网络" },
        { "Connection", "连接" },
        { "Response", "响应" },
        { "Request", "请求" },
        { "Close", "关闭" },
        { "Open", "打开" },
        { "Confirm", "确认" },
        { "Cancel", "取消" },
        { "Refresh", "刷新" },
        { "Save", "保存" },
        { "Delete", "删除" },
        { "Claim", "领取" },
        { "Retry", "重试" },
        { "Loading", "读取中" },
        { "Saving", "保存中" },
        { "Success", "成功" },
        { "Failed", "失败" },
        { "Error", "错误" },
        { "DEFAULT", "默认" },
        { "Default", "默认" },
        { "default", "默认" },
        { "Instant", "立即完成" },
        { "Waiting", "等待中" },
        { "Ready", "可收取" },
        { "None", "无" },
    };

    private static readonly Dictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "ACTIVE", "正常" },
        { "INACTIVE", "未启用" },
        { "BANNED", "账号受限" },
        { "DISABLED", "已停用" },
        { "SUSPENDED", "暂时受限" },
        { "CLOSED", "已关闭" },
        { "CANCELLED", "已取消" },
        { "DELETED", "已注销" },
        { "DELETION_PENDING", "注销申请中" },
        { "AUTHENTICATED", "已认证" },
        { "UNAUTHENTICATED", "未认证" },
        { "VERIFIED", "已认证" },
        { "UNVERIFIED", "未认证" },
        { "ADULT", "成年人" },
        { "MINOR", "未成年人" },
        { "UNKNOWN", "待确认" },
        { "PENDING", "待处理" },
        { "PROCESSING", "处理中" },
        { "IN_PROGRESS", "进行中" },
        { "INPROGRESS", "进行中" },
        { "SUCCESS", "已完成" },
        { "FAILED", "未完成" },
        { "FAILURE", "未完成" },
        { "REJECTED", "未通过" },
        { "REJECT", "未通过" },
        { "APPROVED", "已通过" },
        { "COMPLETED", "已完成" },
        { "EXPIRED", "已过期" },
        { "PAID", "已支付" },
        { "PAYING", "支付中" },
        { "CREATED", "待支付" },
        { "DELIVERED", "已到账" },
        { "REFUNDED", "已退款" },
        { "MANUAL_REVIEW", "审核中" },
        { "ONLINE", "在线" },
        { "OFFLINE", "离线" },
        { "EQUIPPED", "已装备" },
        { "UNEQUIPPED", "未装备" },
        { "AVAILABLE", "可用" },
        { "UNAVAILABLE", "暂不可用" },
        { "LOCKED", "未解锁" },
        { "UNLOCKED", "已解锁" },
    };

    private static readonly Regex TranslationPattern = BuildTranslationPattern();
    private static readonly Regex ProtectedContent = new Regex(@"(<[^>]*>|https?://[^\s<>]+|[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,})", RegexOptions.CultureInvariant);
    private static readonly Regex QuantityPattern = new Regex(@"(?<![A-Za-z0-9])[xX](?=\d)", RegexOptions.CultureInvariant);
    private static readonly Regex DiagnosticPattern = new Regex(@"(?:\b(?:HTTP|ProtocolError|ConnectionError|DataProcessingError|Exception|StackTrace|NullReference|UnityWebRequest|Backend|JsonUtility|prefab|idempotency|SQL|token|TraceId)\b|\b(?:code|result|body|equipment|playerId|sessionId|configId)\s*=|/api/|后端|幂等|预制体|组件缺失|管理器缺失|配置缺失|缺少配置|缺少.*配置|配置无效|接口|解析|请求状态|错误码|网络请求\s*\d{3}|服务器会话|响应为空|空响应|服务器返回|响应异常|响应格式)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        string[] segments = ProtectedContent.Split(value);
        for (int index = 0; index < segments.Length; index += 2)
        {
            segments[index] = TranslationPattern.Replace(segments[index], match => Translations[match.Value]);
            segments[index] = QuantityPattern.Replace(segments[index], "×");
        }
        return string.Concat(segments);
    }

    public static string DisplayStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "待确认";
        string status = value.Trim();
        if (StatusLabels.TryGetValue(status, out string label)) return label;
        return Regex.IsMatch(status, @"^[\u3400-\u9fff\s·，。、（）()\d]+$") ? status : "待确认";
    }

    // Some older catalogue entries and success responses include transport status in their display text.
    private const string SyncData = @"(?:配置|数据|资源|资料|目录|状态|进度|规则|清单|背包|装备|奖池|奖励|账户余额)";
    private const string SyncServer = @"(?:服务器|服务端|后端)";
    private static readonly Regex SyncNotice = new Regex(
        @"(?:^|(?<=[，,。；;\r\n]))[ \t]*(?:" +
        @"(?:已从|已与|来自|由)?" + SyncServer + @"(?:最新)?(?:" + SyncData + @")*(?:已(?:经)?)?(?:同步|加载|读取)(?:完成|成功)?(?:了)?(?:" + SyncData + @")*" +
        @"|(?:当前|最新)?(?:" + SyncData + @")+(?:已(?:经)?(?:与" + SyncServer + @")?(?:同步|加载|读取)|(?:同步|加载|读取)(?:成功|完成))" +
        @"|已(?:同步|加载|读取)(?:了)?(?:" + SyncServer + @")?(?:" + SyncData + @")+" +
        @"|(?:" + SyncData + @")+(?:均)?由" + SyncServer + @"(?:同步|下发|维护)" +
        @"|(?:server|backend) (?:config(?:uration)?|data|catalog) (?:synced|synchronized|loaded)(?: successfully)?" +
        @")(?=[ \t]*(?:$|[，,。；;\r\n]))[ \t]*[，,。；;\r\n]*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string RemoveSyncNotices(string value)
    {
        if (string.IsNullOrEmpty(value) || !SyncNotice.IsMatch(value)) return value;
        return SyncNotice.Replace(value, string.Empty).Trim().TrimEnd('，', ',', '；', ';');
    }

    public static string NormalizePlayerMessage(string message, string fallback = "暂时无法完成，请稍后重试。")
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback ?? string.Empty;
        string text = message.Trim();
        text = Regex.Replace(text, @"^\[\d+\]\s*", string.Empty);
        text = RemoveSyncNotices(text);
        if (text.Length == 0) return string.Empty;
        if (Regex.IsMatch(text, @"[\u3400-\u9fff]") && !DiagnosticPattern.IsMatch(text)) return text;
        string usefulReason = PreserveBusinessReason(text);
        if (!string.IsNullOrEmpty(usefulReason)) return usefulReason;
        if (Regex.IsMatch(text, @"unresolved|reconcil|对账|结果.*(?:未确认|待确认|不明确)|尚未确认.*结果", RegexOptions.IgnoreCase))
            return "操作结果仍在确认，请稍后查看。";
        if (Regex.IsMatch(text, @"(?:session|会话).*(?:changed|切换|变化)|was superseded", RegexOptions.IgnoreCase))
            return "登录状态已变化，请重新打开此页面。";
        if (Regex.IsMatch(text, @"not authenticated|unauthorized|未登录|会话未认证|登录.*(?:失效|过期)|\bHTTP\s*401\b", RegexOptions.IgnoreCase))
            return "登录已失效，请重新登录。";
        if (Regex.IsMatch(text, @"\bHTTP\s*429\b|too many requests", RegexOptions.IgnoreCase))
            return "操作频繁，请稍后再试。";
        if (Regex.IsMatch(text, @"timed?\s*out|timeout|连接超时|请求超时", RegexOptions.IgnoreCase))
            return "连接超时，请稍后查看操作结果。";
        if (Regex.IsMatch(text, @"cannot resolve|could not resolve|cannot connect|failed to connect|Network error|ConnectionError|连接错误|无法连接|网络连接失败", RegexOptions.IgnoreCase))
            return "网络连接不稳定，请检查网络后重试。";
        if (DiagnosticPattern.IsMatch(text)) return fallback ?? string.Empty;
        string normalized = Normalize(text);
        if (DiagnosticPattern.IsMatch(normalized)) return fallback ?? string.Empty;
        if (!Regex.IsMatch(normalized, @"[\u3400-\u9fff]")) return fallback ?? string.Empty;
        return normalized;
    }

    private static string PreserveBusinessReason(string value)
    {
        List<string> reasons = new List<string>();
        foreach (string sentence in Regex.Split(value, @"[。；;\r\n]+"))
        {
            string cleaned = Regex.Replace(sentence, @"(?:\bHTTP\s*[:=]?\s*\d{3}\b|\b(?:code|http)\s*=\s*\d+|\bresult\s*=\s*(?:ProtocolError|ConnectionError|DataProcessingError)|\b(?:ProtocolError|ConnectionError|DataProcessingError)\b)", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"[：:,，]\s*[：:,，]+", "：").Trim(' ', '，', ',', '：', ':');
            if (!DiagnosticPattern.IsMatch(cleaned) && Regex.IsMatch(cleaned, @"[\u3400-\u9fff]")) reasons.Add(cleaned);
        }
        return reasons.Count == 0 ? string.Empty : string.Join("。", reasons) + "。";
    }

    public static bool ContainsAsciiLetter(string value)
    {
        return !string.IsNullOrEmpty(value) && Regex.IsMatch(value, "[A-Za-z]");
    }

    private static Regex BuildTranslationPattern()
    {
        List<string> keys = new List<string>(Translations.Keys);
        keys.Sort((left, right) => right.Length.CompareTo(left.Length));
        List<string> patterns = new List<string>(keys.Count);
        foreach (string key in keys)
        {
            string prefix = IsAsciiTokenCharacter(key[0]) ? @"(?<![A-Za-z0-9_])" : string.Empty;
            string suffix = IsAsciiTokenCharacter(key[key.Length - 1]) ? @"(?![A-Za-z_])" : string.Empty;
            patterns.Add(prefix + Regex.Escape(key) + suffix);
        }
        return new Regex(string.Join("|", patterns), RegexOptions.CultureInvariant);
    }

    private static bool IsAsciiTokenCharacter(char character)
    {
        return character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z'
            || character >= '0' && character <= '9' || character == '_';
    }
}
