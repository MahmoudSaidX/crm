import { TranslationResources } from '@squad-crm/platform';

export const QUICK_REPLY_TRANSLATIONS: TranslationResources = {
  en: {
    'quickReplies.title': 'Quick replies',
    'quickReplies.new': 'New quick reply',
    'quickReplies.edit': 'Edit quick reply',
    'quickReplies.fields.name': 'Name',
    'quickReplies.fields.scope': 'Scope',
    'quickReplies.fields.arabicContent': 'Arabic content',
    'quickReplies.fields.englishContent': 'English content',
    'quickReplies.fields.languages': 'Languages',
    'quickReplies.fields.status': 'Status',
    'quickReplies.scope.global': 'Global',
    'quickReplies.scope.personal': 'Personal',
    'quickReplies.scope.hint':
      'Scope is fixed when the quick reply is created and cannot be changed afterwards.',
    'quickReplies.language.arabic': 'AR',
    'quickReplies.language.english': 'EN',
    'quickReplies.empty': 'No quick replies yet.',
    'quickReplies.validation.name': 'Name is required (max 200 characters).',
    'quickReplies.validation.content':
      'Arabic or English content is required (max 4000 characters).',
    'quickReplies.errors.duplicateName':
      'A quick reply with this name already exists in this scope.',
    'quickReplies.errors.globalPermissionRequired':
      'You do not have permission to manage global quick replies.',
    'quickReplies.errors.notOwner': 'You can only edit your own personal quick replies.',
  },
  ar: {
    'quickReplies.title': 'الردود السريعة',
    'quickReplies.new': 'رد سريع جديد',
    'quickReplies.edit': 'تعديل الرد السريع',
    'quickReplies.fields.name': 'الاسم',
    'quickReplies.fields.scope': 'النطاق',
    'quickReplies.fields.arabicContent': 'المحتوى بالعربية',
    'quickReplies.fields.englishContent': 'المحتوى بالإنجليزية',
    'quickReplies.fields.languages': 'اللغات',
    'quickReplies.fields.status': 'الحالة',
    'quickReplies.scope.global': 'عام',
    'quickReplies.scope.personal': 'شخصي',
    'quickReplies.scope.hint': 'يتم تحديد النطاق عند الإنشاء ولا يمكن تغييره بعد ذلك.',
    'quickReplies.language.arabic': 'ع',
    'quickReplies.language.english': 'EN',
    'quickReplies.empty': 'لا توجد ردود سريعة بعد.',
    'quickReplies.validation.name': 'الاسم مطلوب (بحد أقصى 200 حرف).',
    'quickReplies.validation.content': 'المحتوى بالعربية أو بالإنجليزية مطلوب (بحد أقصى 4000 حرف).',
    'quickReplies.errors.duplicateName': 'يوجد رد سريع بهذا الاسم في هذا النطاق بالفعل.',
    'quickReplies.errors.globalPermissionRequired': 'ليس لديك صلاحية إدارة الردود السريعة العامة.',
    'quickReplies.errors.notOwner': 'يمكنك تعديل ردودك السريعة الشخصية فقط.',
  },
};
