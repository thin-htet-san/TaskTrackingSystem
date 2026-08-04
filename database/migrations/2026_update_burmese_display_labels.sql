BEGIN;

-- Localized display labels only. Stable menu codes and English values remain unchanged.
UPDATE "Menus"
SET "MenuNameMy" = 'ပင်မစာမျက်နှာ'
WHERE "MenuCode" IN ('DASHBOARD', 'DASHBOARD_ADMIN', 'DASHBOARD_MANAGER', 'DASHBOARD_EMPLOYEE');

UPDATE "Menus"
SET "MenuNameMy" = 'အသုံးပြုသူ'
WHERE "MenuCode" = 'USERS';

-- The default admin account is displayed as a Burmese name in Burmese UI.
UPDATE "Users"
SET "FirstNameMy" = 'စနစ်',
    "LastNameMy" = 'အုပ်ချုပ်သူ'
WHERE "Username" = 'admin'
  AND "FirstName" = 'System'
  AND "LastName" = 'Admin';

COMMIT;
