namespace SquadCrm.Modules.TicketManagement.DemoData;

/// <summary>
/// Realistic Arabic/English subject and description pairs for demo tickets, tied
/// to the demo category codes so a ticket's text matches its classification.
/// Deliberately a small fixed pool rather than a generator library.
/// </summary>
public static class DemoTicketContent
{
    public sealed record Topic(string CategoryCode, string Subject, string Description);

    public static readonly Topic[] Topics =
    [
        new("account", "Unable to access account",
            "The customer cannot sign in after changing the registered mobile number. "
            + "The password reset email does not arrive."),
        new("account", "تعذر الدخول إلى الحساب",
            "العميل لا يستطيع تسجيل الدخول بعد تحديث رقم الجوال، ولم تصله رسالة إعادة تعيين كلمة المرور."),
        new("account", "Account verification issue",
            "Identity verification keeps failing even though the uploaded document is valid and unexpired."),
        new("billing", "Payment not reflected",
            "The customer paid by bank transfer two days ago; the invoice still shows as unpaid."),
        new("billing", "استفسار عن الفاتورة",
            "العميل يستفسر عن رسوم إضافية ظهرت في فاتورة هذا الشهر ويطلب توضيحًا مفصلًا."),
        new("billing", "Billing inquiry — duplicate charge",
            "Two identical charges appear on the same day. The customer requests a refund for one of them."),
        new("delivery", "Delivery delayed",
            "The shipment has not moved for four days and the tracking page shows no update."),
        new("delivery", "تأخر التوصيل",
            "الشحنة لم تصل في الموعد المحدد والعميل يطلب تحديثًا عن الحالة."),
        new("technical-issue", "Mobile application issue",
            "The application closes unexpectedly when opening the orders tab on Android 12."),
        new("technical-issue", "مشكلة في التطبيق",
            "التطبيق يظهر شاشة بيضاء عند فتح صفحة الطلبات على بعض الأجهزة."),
        new("technical-issue", "Report export fails",
            "Exporting the monthly report returns an error after roughly thirty seconds."),
        new("service-request", "Service request follow-up",
            "The customer is following up on a service activation request raised last week."),
        new("service-request", "طلب تفعيل خدمة",
            "العميل يطلب تفعيل خدمة إضافية على حسابه الحالي."),
        new("complaint", "Incorrect customer information",
            "The registered address on the profile belongs to a previous customer and must be corrected."),
        new("complaint", "شكوى بخصوص جودة الخدمة",
            "العميل غير راضٍ عن مدة الانتظار في آخر مكالمة دعم ويطلب المتابعة."),
        new("general-inquiry", "Question about working hours",
            "The customer asks whether the branch operates during the upcoming holiday."),
        new("general-inquiry", "استفسار عام عن الخدمات",
            "العميل يستفسر عن الخدمات المتاحة في فرع المدينة."),
    ];

    /// <summary>The topic for the ticket at <paramref name="index"/>.</summary>
    public static Topic At(int index) => Topics[index % Topics.Length];
}
