namespace SquadCrm.Modules.SystemConfiguration;

public enum ConfigurationValueType
{
    String,
    Number,
    Boolean,
}

/// <summary>
/// A single explicitly registered, administratively editable configuration
/// key. Only keys defined here are browsable/editable from the admin
/// screen — the UI cannot create arbitrary application/environment keys.
/// </summary>
public sealed record ConfigurationDefinition(
    string Key,
    ConfigurationValueType ValueType,
    string DisplayNameEn,
    string DisplayNameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    string DefaultValue,
    bool IsSensitive,
    bool RequiresRestart,
    int? MinNumber = null,
    int? MaxNumber = null);

/// <summary>
/// The registered configuration key catalog. Ownership of a key's meaning
/// stays with the capability it describes; this module only provides the
/// shared storage/read/admin mechanics per the CRM-115 Deadline Acceptance
/// Override — dynamic configuration distribution and a full feature-flag
/// platform are stretch/non-blocking.
/// </summary>
public static class ConfigurationCatalog
{
    public static readonly IReadOnlyList<ConfigurationDefinition> Definitions =
    [
        new ConfigurationDefinition(
            "general.company_display_name",
            ConfigurationValueType.String,
            "Company display name",
            "اسم الشركة المعروض",
            "Shown in the application shell and outgoing communications.",
            "يظهر في واجهة التطبيق والمراسلات الصادرة.",
            DefaultValue: "Squad CRM",
            IsSensitive: false,
            RequiresRestart: false),
        new ConfigurationDefinition(
            "tickets.default_page_size",
            ConfigurationValueType.Number,
            "Default ticket page size",
            "حجم صفحة التذاكر الافتراضي",
            "Number of tickets shown per page by default.",
            "عدد التذاكر المعروضة افتراضيًا في كل صفحة.",
            DefaultValue: "25",
            IsSensitive: false,
            RequiresRestart: false,
            MinNumber: 10,
            MaxNumber: 100),
        new ConfigurationDefinition(
            "notifications.email_enabled",
            ConfigurationValueType.Boolean,
            "Email notifications enabled",
            "تفعيل إشعارات البريد الإلكتروني",
            "Whether outgoing email notifications are sent.",
            "ما إذا كانت إشعارات البريد الإلكتروني الصادرة سيتم إرسالها.",
            DefaultValue: "true",
            IsSensitive: false,
            RequiresRestart: false),
        new ConfigurationDefinition(
            "integrations.smtp_password",
            ConfigurationValueType.String,
            "SMTP password",
            "كلمة مرور SMTP",
            "Secret credential for the outbound email provider. Never returned once set.",
            "بيانات اعتماد سرية لمزود البريد الصادر. لا تُعاد بعد ضبطها.",
            DefaultValue: "",
            IsSensitive: true,
            RequiresRestart: true),
    ];

    public static ConfigurationDefinition? Find(string key) =>
        Definitions.FirstOrDefault(definition => definition.Key == key);
}
