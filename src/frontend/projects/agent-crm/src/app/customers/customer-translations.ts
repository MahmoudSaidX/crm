import { TranslationResources } from '@squad-crm/platform';

export const CUSTOMER_TRANSLATIONS: TranslationResources = {
  en: {
    'customers.new': 'New customer',
    'customers.fields.firstName': 'First name',
    'customers.fields.lastName': 'Last name',
    'customers.fields.preferredLanguage': 'Preferred language',
    'customers.fields.department': 'Department',
    'customers.fields.branch': 'Branch',
    'customers.language.arabic': 'Arabic',
    'customers.language.english': 'English',
    'customers.validation.firstName': 'First name is required (max 200 characters).',
    'customers.validation.lastName': 'Last name is required (max 200 characters).',
    'customers.errors.duplicateCustomer': 'A matching customer already exists.',
    'customers.errors.inactiveDepartment': 'The selected department is not active.',
    'customers.errors.inactiveBranch': 'The selected branch is not active.',
  },
  ar: {
    'customers.new': 'عميل جديد',
    'customers.fields.firstName': 'الاسم الأول',
    'customers.fields.lastName': 'اسم العائلة',
    'customers.fields.preferredLanguage': 'اللغة المفضلة',
    'customers.fields.department': 'القسم',
    'customers.fields.branch': 'الفرع',
    'customers.language.arabic': 'العربية',
    'customers.language.english': 'الإنجليزية',
    'customers.validation.firstName': 'الاسم الأول مطلوب (بحد أقصى 200 حرف).',
    'customers.validation.lastName': 'اسم العائلة مطلوب (بحد أقصى 200 حرف).',
    'customers.errors.duplicateCustomer': 'يوجد عميل مطابق بالفعل.',
    'customers.errors.inactiveDepartment': 'القسم المحدد غير نشط.',
    'customers.errors.inactiveBranch': 'الفرع المحدد غير نشط.',
  },
};
