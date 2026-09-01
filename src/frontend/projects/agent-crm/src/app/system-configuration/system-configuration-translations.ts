import { TranslationResources } from '@squad-crm/platform';

export const SYSTEM_CONFIGURATION_TRANSLATIONS: TranslationResources = {
  en: {
    'systemConfiguration.title': 'System Configuration',
    'systemConfiguration.fields.name': 'Setting',
    'systemConfiguration.fields.description': 'Description',
    'systemConfiguration.fields.value': 'Value',
    'systemConfiguration.requiresRestart': 'Requires restart',
    'systemConfiguration.sensitive.set': 'Set',
    'systemConfiguration.sensitive.notSet': 'Not set',
    'systemConfiguration.empty': 'No configuration keys are registered.',
    'systemConfiguration.errors.invalidValue': 'The value is not valid for this setting.',
  },
  ar: {
    'systemConfiguration.title': 'إعدادات النظام',
    'systemConfiguration.fields.name': 'الإعداد',
    'systemConfiguration.fields.description': 'الوصف',
    'systemConfiguration.fields.value': 'القيمة',
    'systemConfiguration.requiresRestart': 'يتطلب إعادة التشغيل',
    'systemConfiguration.sensitive.set': 'مضبوطة',
    'systemConfiguration.sensitive.notSet': 'غير مضبوطة',
    'systemConfiguration.empty': 'لا توجد إعدادات مسجلة.',
    'systemConfiguration.errors.invalidValue': 'القيمة غير صالحة لهذا الإعداد.',
  },
};
