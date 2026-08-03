BEGIN;

UPDATE "Menus" SET "MenuNameMy" = 'ဒက်ရှ်ဘုတ်' WHERE "MenuCode" IN ('DASHBOARD', 'DASHBOARD_ADMIN', 'DASHBOARD_MANAGER', 'DASHBOARD_EMPLOYEE');
UPDATE "Menus" SET "MenuNameMy" = 'စီမံကိန်းများ' WHERE "MenuCode" = 'PROJECTS';
UPDATE "Menus" SET "MenuNameMy" = 'စီမံကိန်းစာရင်း' WHERE "MenuCode" = 'PROJECTS_LIST';
UPDATE "Menus" SET "MenuNameMy" = 'စီမံကိန်း ခွဲဝေခြင်း' WHERE "MenuCode" = 'PROJECTS_ASSIGN';
UPDATE "Menus" SET "MenuNameMy" = 'လုပ်ငန်းများ' WHERE "MenuCode" = 'TASKS';
UPDATE "Menus" SET "MenuNameMy" = 'လုပ်ငန်းစာရင်း' WHERE "MenuCode" = 'TASKS_LIST';
UPDATE "Menus" SET "MenuNameMy" = 'ကန်ဘန်းဘုတ်' WHERE "MenuCode" = 'TASKS_BOARD';
UPDATE "Menus" SET "MenuNameMy" = 'လုပ်ငန်း ခွဲဝေခြင်း' WHERE "MenuCode" = 'TASKS_ASSIGN';
UPDATE "Menus" SET "MenuNameMy" = 'လုပ်ငန်း နောက်ခံစာရင်း' WHERE "MenuCode" = 'TASKS_BACKLOG';
UPDATE "Menus" SET "MenuNameMy" = 'အစီရင်ခံစာများ' WHERE "MenuCode" = 'REPORTS';
UPDATE "Menus" SET "MenuNameMy" = 'လုပ်ငန်း အစီရင်ခံစာ' WHERE "MenuCode" = 'REPORTS_TASKS';
UPDATE "Menus" SET "MenuNameMy" = 'အချိန်မှတ်တမ်း' WHERE "MenuCode" = 'REPORTS_TIMESHEET';
UPDATE "Menus" SET "MenuNameMy" = 'နောက်ကျ လုပ်ငန်းများ' WHERE "MenuCode" = 'REPORTS_OVERDUE';
UPDATE "Menus" SET "MenuNameMy" = 'ဝန်ထမ်း အစီရင်ခံစာ' WHERE "MenuCode" = 'REPORTS_EMPLOYEES';
UPDATE "Menus" SET "MenuNameMy" = 'စီမံကိန်း တိုးတက်မှု' WHERE "MenuCode" = 'REPORTS_PROJECTS';
UPDATE "Menus" SET "MenuNameMy" = 'အသုံးပြုသူများ' WHERE "MenuCode" = 'USERS';
UPDATE "Menus" SET "MenuNameMy" = 'အခန်းကဏ္ဍများ' WHERE "MenuCode" = 'ROLES';
UPDATE "Menus" SET "MenuNameMy" = 'အခန်းကဏ္ဍ အပြင်အဆင်များ' WHERE "MenuCode" = 'DASHBOARD_WIDGETS';
UPDATE "Menus" SET "MenuNameMy" = 'ပြဿနာ ထည့်ရန်' WHERE "MenuCode" = 'ISSUES_ADD';
UPDATE "Menus" SET "MenuNameMy" = 'ပြဿနာ စာရင်း' WHERE "MenuCode" = 'ISSUES_LIST';
UPDATE "Menus" SET "MenuNameMy" = 'စနစ်မှတ်တမ်းများ' WHERE "MenuCode" = 'AUDIT_LOGS';
UPDATE "Menus" SET "MenuNameMy" = 'ပြဿနာ အစီရင်ခံစာ' WHERE "MenuCode" = 'REPORTS_ISSUES';

UPDATE "Permissions" SET "ActionNameMy" = 'စာရင်းကြည့်ရန်'
WHERE "PermissionCode" IN ('Projects_List', 'Tasks_List', 'Users_List', 'Roles_List', 'Issues_List', 'AuditLogs_List');
UPDATE "Permissions" SET "ActionNameMy" = 'ဖန်တီးရန်'
WHERE "PermissionCode" IN ('Projects_Create', 'Tasks_Create', 'Users_Create', 'Roles_Create', 'Issues_Create');
UPDATE "Permissions" SET "ActionNameMy" = 'ပြင်ဆင်ရန်'
WHERE "PermissionCode" IN ('Projects_Update', 'Tasks_Update', 'Users_Update', 'Roles_Update', 'Issues_Update');
UPDATE "Permissions" SET "ActionNameMy" = 'ဖျက်ရန်'
WHERE "PermissionCode" IN ('Projects_Delete', 'Tasks_Delete', 'Users_Delete', 'Roles_Delete', 'Issues_Delete');

UPDATE "Roles" SET "NameMy" = 'စီမံခန့်ခွဲသူ', "DescriptionMy" = 'စနစ်အပြည့်အဝ အသုံးပြုခွင့်' WHERE "Name" = 'Admin';
UPDATE "Roles" SET "NameMy" = 'မန်နေဂျာ', "DescriptionMy" = 'စီမံကိန်းအလိုက် စီမံခန့်ခွဲသူ' WHERE "Name" = 'Manager';
UPDATE "Roles" SET "NameMy" = 'ဝန်ထမ်း', "DescriptionMy" = 'လုပ်ငန်းအဆင့် အသုံးပြုခွင့်' WHERE "Name" = 'Employee';

COMMIT;
