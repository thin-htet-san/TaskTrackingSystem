-- Burmese translation review sample generated from the original English CSV exports.
-- Scope: 10 Projects, 15 Tasks, 5 Issues.
-- This is a review sample only. It was not executed and does not replace the prior SQL file.

BEGIN;

-- Projects: NameMy
UPDATE "Projects"
SET "NameMy" = v."Value"
FROM (
    VALUES
        (1, 'ရန်ကုန် ဖောက်သည်ပေါ်တယ်'), -- Source record ID: 1
        (2, 'မန္တလေး ကုန်ပစ္စည်းစာရင်းစနစ်'), -- Source record ID: 2
        (3, 'နေပြည်တော် HR ခြေရာခံစနစ်'), -- Source record ID: 3
        (4, 'ပဲခူး ဝယ်ယူရေးစီမံခန့်ခွဲမှုစနစ်'), -- Source record ID: 4
        (5, 'တောင်ကြီး အကူအညီပေးရေးစနစ်'), -- Source record ID: 5
        (6, 'မော်လမြိုင် ထောက်ပံ့ရေးစီမံခန့်ခွဲမှုစနစ်'), -- Source record ID: 6
        (7, 'ပုသိမ် ပင်လယ်စာရောင်းဝယ်ရေးပေါ်တယ်'), -- Source record ID: 7
        (8, 'မုံရွာ ငွေတောင်းခံမှုစနစ်'), -- Source record ID: 8
        (9, 'စစ်တွေ ဆိပ်ကမ်း ပင်မစာမျက်နှာ'), -- Source record ID: 9
        (10, 'ပြင်ဦးလွင် စမတ်စိုက်ပျိုးရေးစနစ်') -- Source record ID: 10
) AS v("Id", "Value")
WHERE "Projects"."Id" = v."Id"
  AND ("NameMy" IS NULL OR btrim("NameMy") = '');

-- Projects: DescriptionMy
UPDATE "Projects"
SET "DescriptionMy" = v."Value"
FROM (
    VALUES
        (1, 'ရန်ကုန်ရှိ ရုံးခွဲများအတွက် ဖောက်သည်ဝန်ဆောင်မှုပေါ်တယ်'), -- Source record ID: 1
        (2, 'မန္တလေးအဖွဲ့အတွက် ဂိုဒေါင်နှင့် ကုန်ပစ္စည်းများကို ခြေရာခံသည့်စနစ်'), -- Source record ID: 2
        (3, 'HR လုပ်ငန်းစဉ်များနှင့် ခွင့်ရက်များကို ခြေရာခံသည့်စနစ်'), -- Source record ID: 3
        (4, 'ဝယ်ယူရန်တောင်းဆိုမှုများနှင့် အတည်ပြုခြင်းလုပ်ငန်းစဉ်'), -- Source record ID: 4
        (5, 'အကူအညီတောင်းခံမှုနှင့် ပြဿနာများကို ခြေရာခံသည့် ပင်မစာမျက်နှာ'), -- Source record ID: 5
        (6, 'ထောက်ပံ့ရေးကွင်းဆက်ကို ခြေရာခံသည့်စနစ်'), -- Source record ID: 6
        (7, 'လုပ်ငန်းများအကြား ပင်လယ်စာရောင်းဝယ်ရန် အွန်လိုင်းစျေးကွက်ပလက်ဖောင်း'), -- Source record ID: 7
        (8, 'အသုံးဝင်မှုဝန်ဆောင်ခများကို အလိုအလျောက် ငွေတောင်းခံသည့်စနစ်'), -- Source record ID: 8
        (9, 'သင်္ဘောနှင့် ကုန်ပစ္စည်းများကို ခြေရာခံရန် UI'), -- Source record ID: 9
        (10, 'စမတ်စိုက်ပျိုးရေးဆိုင်ရာ အချက်အလက်ခွဲခြမ်းစိတ်ဖြာမှု ပင်မစာမျက်နှာ') -- Source record ID: 10
) AS v("Id", "Value")
WHERE "Projects"."Id" = v."Id"
  AND ("DescriptionMy" IS NULL OR btrim("DescriptionMy") = '');

-- Tasks: TitleMy
UPDATE "Tasks"
SET "TitleMy" = v."Value"
FROM (
    VALUES
        (1, 'ပေါ်တယ်ပင်မစာမျက်နှာ ပြုပြင်ခြင်း'), -- Source record ID: 1
        (2, 'အကောင့်ဝင်ခြင်းနှင့် အခန်းကဏ္ဍအလိုက် လမ်းကြောင်းသတ်မှတ်ခြင်း'), -- Source record ID: 2
        (3, 'ဖောက်သည်ကိုယ်ရေးအချက်အလက် ဖြည့်သွင်းပုံစံ'), -- Source record ID: 3
        (4, 'ငွေပေးချေမှုစာမျက်နှာ ပြုပြင်ရှင်းလင်းခြင်း'), -- Source record ID: 4
        (5, 'အသိပေးချက်ဗဟိုစနစ်'), -- Source record ID: 5
        (6, 'ဖောက်သည်စာရင်း ထုတ်ယူခြင်း'), -- Source record ID: 6
        (7, 'စစ်ဆေးမှတ်တမ်းစာမျက်နှာ'), -- Source record ID: 7
        (8, 'အသွင်အပြင် နောက်ဆုံးပြင်ဆင်ခြင်း'), -- Source record ID: 8
        (9, 'ကုန်ပစ္စည်းလက်ကျန် ပင်မစာမျက်နှာကတ်များ'), -- Source record ID: 9
        (10, 'ဂိုဒေါင်ပစ္စည်း ရှာဖွေခြင်း'), -- Source record ID: 10
        (11, 'ကုန်ပစ္စည်းလက်ကျန်နည်းပါးမှု သတိပေးချက် ပြင်ဆင်ခြင်း'), -- Source record ID: 11
        (12, 'ကုန်ပစ္စည်းလက်ခံ ဖြည့်သွင်းပုံစံ'), -- Source record ID: 12
        (13, 'ကုန်ပစ္စည်း ဘားကုဒ်စာမျက်နှာ'), -- Source record ID: 13
        (14, 'ကုန်ပစ္စည်းစာရင်း အစီရင်ခံစာ ထုတ်ယူခြင်း'), -- Source record ID: 14
        (15, 'ကုန်ပစ္စည်းလက်ကျန် ပြင်ဆင်မှု စစ်ဆေးခြင်း') -- Source record ID: 15
) AS v("Id", "Value")
WHERE "Tasks"."Id" = v."Id"
  AND ("TitleMy" IS NULL OR btrim("TitleMy") = '');

-- Tasks: DescriptionMy
UPDATE "Tasks"
SET "DescriptionMy" = v."Value"
FROM (
    VALUES
        (1, 'ရန်ကုန်ဖောက်သည်ပေါ်တယ်၏ ပင်မစာမျက်နှာကို ပြုပြင်ရှင်းလင်းခြင်း'), -- Source record ID: 1
        (2, 'အကောင့်ဝင်ပြီးနောက် သက်ဆိုင်ရာစာမျက်နှာသို့ ပြန်လည်ညွှန်ပို့သည့် စည်းမျဉ်းများ'), -- Source record ID: 2
        (3, 'ကိုယ်ရေးအချက်အလက်များကို ပြင်ဆင်ခြင်းနှင့် မှန်ကန်မှု စစ်ဆေးခြင်း'), -- Source record ID: 3
        (4, 'ငွေပေးချေမှုအပိုင်းရှိ UI နှင့် စစ်ထုတ်မှုများကို ပြုပြင်ရှင်းလင်းခြင်း'), -- Source record ID: 4
        (5, 'အသိပေးချက်စာရင်းနှင့် မဖတ်ရသေးကြောင်းပြသသည့် အမှတ်အသား'), -- Source record ID: 5
        (6, 'ဖောက်သည်အချက်အလက်များကို CSV / Excel ဖိုင်အဖြစ် ထုတ်ယူခြင်း'), -- Source record ID: 6
        (7, 'လုပ်ဆောင်ချက်မှတ်တမ်းနှင့် ဖြစ်စဉ်မှတ်တမ်းစာမျက်နှာ'), -- Source record ID: 7
        (8, 'နေရာလွတ်နှင့် အရောင်များကို နောက်ဆုံးအဆင့် ပြုပြင်မွမ်းမံခြင်း'), -- Source record ID: 8
        (9, 'ဂိုဒေါင်အကျဉ်းချုပ်ကတ်များနှင့် အရေအတွက်ပြကိန်းများ'), -- Source record ID: 9
        (10, 'ကုန်ပစ္စည်းများကို မြန်ဆန်စွာ ရှာဖွေပြီး စစ်ထုတ်နိုင်ရန်'), -- Source record ID: 10
        (11, 'လက်ကျန်နည်းပါးသည့်အခါ သတိပေးအမှတ်အသားများ ပြသခြင်း'), -- Source record ID: 11
        (12, 'လက်ခံရရှိသော ကုန်ပစ္စည်းများကို မှတ်တမ်းတင်ရန် ဖြည့်သွင်းပုံစံ'), -- Source record ID: 12
        (13, 'ဘားကုဒ်ဖြင့် ရှာဖွေခြင်းနှင့် ကုန်ပစ္စည်းအသေးစိတ်စာမျက်နှာ'), -- Source record ID: 13
        (14, 'စီမံခန့်ခွဲမှုအတွက် ကုန်ပစ္စည်းလက်ကျန်အကျဉ်းချုပ်ကို ထုတ်ယူခြင်း'), -- Source record ID: 14
        (15, 'ပြင်ဆင်ပြောင်းလဲထားသည့် မှတ်တမ်းများကို ပြန်လည်သုံးသပ်ခြင်း') -- Source record ID: 15
) AS v("Id", "Value")
WHERE "Tasks"."Id" = v."Id"
  AND ("DescriptionMy" IS NULL OR btrim("DescriptionMy") = '');

-- Issues: TitleMy
UPDATE "Issues"
SET "TitleMy" = v."Value"
FROM (
    VALUES
        (1, 'API စမ်းသပ်ခြင်း'), -- Source record ID: 1
        (2, 'ကွန်ရက်လုံခြုံရေး တားဆီးကာများကို စစ်ဆေးခြင်း'), -- Source record ID: 2
        (3, 'Yangon Retail Upgrade စီမံကိန်း၏ လက်လီငွေရှင်းလုပ်ငန်းစဉ်အတွက် အသုံးပြုသူလိုအပ်ချက်များကို ပြန်လည်ပြင်ဆင်ခြင်း'), -- Source record ID: 3
        (4, 'Yangon Retail စီမံကိန်းအတွက် POS စနစ်ကို ပေါင်းစည်းအကောင်အထည်ဖော်ခြင်း'), -- Source record ID: 4
        (5, 'Yangon Retail စီမံကိန်း၏ ငွေကြေးအမျိုးအစားစုံ ငွေရှင်းလုပ်ငန်းစဉ်ကို အရည်အသွေးစစ်ဆေးခြင်း') -- Source record ID: 5
) AS v("Id", "Value")
WHERE "Issues"."Id" = v."Id"
  AND ("TitleMy" IS NULL OR btrim("TitleMy") = '');

-- Issues: DescriptionMy
UPDATE "Issues"
SET "DescriptionMy" = v."Value"
FROM (
    VALUES
        (1, 'API စမ်းသပ်မှု'), -- Source record ID: 1
        (3, 'လိုအပ်ချက်ဆွေးနွေးပွဲအပြီး ဆက်လက်လုပ်ဆောင်ရမည့်အလုပ်'), -- Source record ID: 3
        (4, 'အကောင်အထည်ဖော်ရန် ဆက်လက်လုပ်ဆောင်ရမည့်အလုပ်'), -- Source record ID: 4
        (5, 'အတည်ပြုစစ်ဆေးရန် ဆက်လက်လုပ်ဆောင်ရမည့်အလုပ်') -- Source record ID: 5
) AS v("Id", "Value")
WHERE "Issues"."Id" = v."Id"
  AND ("DescriptionMy" IS NULL OR btrim("DescriptionMy") = '');

COMMIT;
