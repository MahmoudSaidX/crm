import { TranslationResources } from '@squad-crm/platform';

export const TASK_CREATE_TRANSLATIONS: TranslationResources = {
  en: {
    'taskCreate.title': 'New task',
    'taskCreate.fields.title': 'Title',
    'taskCreate.fields.details': 'Details',
    'taskCreate.fields.dueAt': 'Due',
    'taskCreate.fields.ticket': 'Linked ticket',
    'taskCreate.fields.customer': 'Linked customer',
    'taskCreate.validation.title': 'Title is required (max 200 characters).',
    'taskCreate.errors.invalidTicket': 'The selected ticket does not exist.',
    'taskCreate.errors.invalidCustomer': 'The selected customer does not exist.',
    'taskCreate.errors.ineligibleOwner': 'The selected owner is not eligible.',
  },
  ar: {
    'taskCreate.title': 'مهمة جديدة',
    'taskCreate.fields.title': 'العنوان',
    'taskCreate.fields.details': 'التفاصيل',
    'taskCreate.fields.dueAt': 'الاستحقاق',
    'taskCreate.fields.ticket': 'التذكرة المرتبطة',
    'taskCreate.fields.customer': 'العميل المرتبط',
    'taskCreate.validation.title': 'العنوان مطلوب (بحد أقصى 200 حرف).',
    'taskCreate.errors.invalidTicket': 'التذكرة المحددة غير موجودة.',
    'taskCreate.errors.invalidCustomer': 'العميل المحدد غير موجود.',
    'taskCreate.errors.ineligibleOwner': 'المالك المحدد غير مؤهل.',
  },
};
