using System.Windows;

namespace Stickel.Desktop;

public static class Localization
{
    public static string Language { get; private set; } = "fa";
    private static readonly Dictionary<string, (string En, string Fa)> Words = new()
    {
        ["New"] = ("New", "جدید"), ["Open"] = ("Open", "بازکردن"), ["Save"] = ("Save", "ذخیره"),
        ["Import"] = ("Import media", "افزودن رسانه"), ["Export"] = ("Export", "خروجی گرفتن"), ["Settings"] = ("SETTINGS", "تنظیمات"),
        ["Preview"] = ("PREVIEW", "پیش‌نمایش"), ["Media"] = ("MEDIA LIBRARY", "پلی‌لیست"), ["Timeline"] = ("TIMELINE", "تایم‌لاین"),
        ["Undo"] = ("Undo", "واگرد"), ["Redo"] = ("Redo", "ازنو"), ["Dark"] = ("Dark", "تیره"), ["Light"] = ("Light", "روشن"),
        ["Resize"] = ("Resize media", "تغییر اندازهٔ مدیا"), ["Rotate"] = ("Rotate media", "چرخش مدیا"),
        ["Up"] = ("Move up", "انتقال به بالا"), ["Down"] = ("Move down", "انتقال به پایین"),
        ["BackSecond"] = ("Back one second", "یک ثانیه عقب"), ["ForwardSecond"] = ("Forward one second", "یک ثانیه جلو"),
        ["PlayAction"] = ("Play · Space", "پخش · Space"), ["PauseAction"] = ("Pause · Space", "مکث · Space"),
        ["ResizeHint"] = ("Drag to resize · keeps aspect ratio", "برای تغییر اندازه بکشید؛ نسبت تصویر حفظ می‌شود"),
        ["RotateHint"] = ("Drag to rotate · Shift: 15° steps", "برای چرخش بکشید؛ با Shift گام‌های ۱۵ درجه"),
        ["Output"] = ("Output", "خروجی"), ["Format"] = ("Format", "فرمت"), ["Telegram"] = ("Telegram sticker", "استیکر تلگرام"),
        ["Free"] = ("Free export", "خروجی آزاد"), ["Quality"] = ("Quality", "کیفیت"), ["AutoQuality"] = ("Auto · best quality under 256 KB", "خودکار · بهترین کیفیت زیر ۲۵۶ کیلوبایت"),
        ["Parts"] = ("Also export each clip separately", "خروجی جداگانه‌ی هر بخش هم ساخته شود"), ["Mute"] = ("Silent output", "خروجی بی‌صدا"),
        ["Canvas"] = ("Canvas & background", "بوم و پس‌زمینه"), ["Width"] = ("Width", "عرض"), ["Height"] = ("Height", "ارتفاع"),
        ["Background"] = ("MP4 background", "پس‌زمینه MP4"), ["ChooseColor"] = ("Choose color", "انتخاب رنگ"),
        ["Crop"] = ("Crop & transform · selected clip", "کراپ و موقعیت · بخش انتخابی"), ["Fit"] = ("Fit", "جا دادن کامل"),
        ["Fill"] = ("Fill / cover", "پرکردن با برش"), ["Stretch"] = ("Stretch", "کشیدن آزاد"), ["Zoom"] = ("Scale", "بزرگ‌نمایی"),
        ["CropDraw"] = ("Draw crop", "کشیدن کادر کراپ"), ["ResetCrop"] = ("Reset", "بازنشانی"), ["CropX"] = ("Crop X %", "شروع افقی ٪"),
        ["CropY"] = ("Crop Y %", "شروع عمودی ٪"), ["CropW"] = ("Crop width %", "عرض کراپ ٪"), ["CropH"] = ("Crop height %", "ارتفاع کراپ ٪"),
        ["Watermark"] = ("Global watermark", "واترمارک مشترک"), ["ChooseWatermark"] = ("Choose image", "انتخاب تصویر"), ["EnableWatermark"] = ("Show watermark", "نمایش واترمارک"),
        ["Opacity"] = ("Transparency %", "شفافیت ٪"), ["WmSize"] = ("Size · % of canvas width", "اندازه · درصد عرض بوم"),
        ["Selected"] = ("Selected clip", "بخش انتخاب‌شده"), ["Whole"] = ("Whole timeline", "کل تایم‌لاین"),
        ["Play"] = ("Play / Pause", "پخش / مکث"), ["Stop"] = ("Stop", "توقف"), ["Center"] = ("Center media", "وسط‌چین رسانه"),
        ["PrevFrame"] = ("Previous frame", "فریم قبلی"), ["NextFrame"] = ("Next frame", "فریم بعدی"),
        ["Videos"] = ("Video / animated", "ویدیو / متحرک"), ["Images"] = ("Images", "تصاویر"), ["AddClip"] = ("Add to timeline", "افزودن به تایم‌لاین"),
        ["RemoveMedia"] = ("Remove from library", "حذف از پلی‌لیست"), ["DeleteClip"] = ("Remove clip", "حذف بخش"), ["Left"] = ("Move left", "انتقال به چپ"), ["Right"] = ("Move right", "انتقال به راست"),
        ["TrimIn"] = ("In (seconds)", "شروع (ثانیه)"), ["TrimOut"] = ("Out (seconds)", "پایان (ثانیه)"), ["Duration"] = ("Duration (seconds)", "مدت (ثانیه)"),
        ["ApplyTrim"] = ("Apply trim / duration", "اعمال تریم / مدت"), ["Cancel"] = ("Cancel", "لغو"), ["Ready"] = ("Ready · all processing stays on this computer", "آماده · پردازش‌ها روی همین کامپیوتر انجام می‌شوند"),
        ["EmptyMedia"] = ("Drop files here or import media.\nDouble-click a file to add it to the timeline.", "فایل‌ها را اینجا رها کن یا رسانه اضافه کن.\nبا دوبار کلیک، فایل به تایم‌لاین اضافه می‌شود."),
        ["EmptyPreview"] = ("Your next sticker starts here", "استیکر بعدی تو از اینجا شروع می‌شود"),
        ["EmptyTimeline"] = ("Drag media here to start editing", "برای شروع، رسانه را به این نوار بکش"),
        ["DragHint"] = ("Drag media or watermark in the preview · Ctrl+Z / Ctrl+Shift+Z", "رسانه و واترمارک را با ماوس جابه‌جا کن · Ctrl+Z / Ctrl+Shift+Z"),
        ["Valid"] = ("Telegram duration valid · up to 3s", "مدت مجاز تلگرام · حداکثر ۳ ثانیه"), ["Invalid"] = ("Over 3s · Telegram export is blocked", "بیشتر از ۳ ثانیه · خروجی تلگرام متوقف می‌شود"),
        ["Importing"] = ("Importing media…", "در حال ورود رسانه…"), ["Saving"] = ("Packing project and original media…", "در حال فشرده‌سازی پروژه و رسانه‌های اصلی…"),
        ["Opening"] = ("Opening project…", "در حال بازکردن پروژه…"), ["Exporting"] = ("Rendering / encoding…", "در حال رندر / ساخت خروجی…"),
        ["Preparing"] = ("Preparing smooth preview…", "در حال آماده‌سازی پیش‌نمایش روان…"), ["Error"] = ("Operation failed", "عملیات انجام نشد"),
        ["Saved"] = ("Project saved", "پروژه ذخیره شد"), ["Cancelled"] = ("Cancelled", "لغو شد"), ["Complete"] = ("Export complete", "خروجی ساخته شد"),
        ["Unsaved"] = ("Save your changes before continuing?", "تغییرات پروژه پیش از ادامه ذخیره شوند؟"), ["Estimate"] = ("Size estimate", "برآورد حجم"),
        ["StaticHint"] = ("Timeline: current frame · parts: first trimmed frame", "تایم‌لاین: فریم فعلی · هر بخش: اولین فریم پس از تریم"),
        ["NoSelection"] = ("Select a clip to edit", "یک بخش را برای ویرایش انتخاب کن"), ["FrameCropHint"] = ("Drag a rectangle to crop the source", "برای کراپ، روی تصویر یک مستطیل بکش"),
        ["SaveAs"] = ("Save project as…", "ذخیره پروژه با نام…"), ["ShowFolder"] = ("Open output folder", "بازکردن پوشه خروجی"),
        ["AudioMonitor"] = ("Preview volume", "بلندی صدای پیش‌نمایش"), ["MutePreview"] = ("Mute preview", "بی‌صدا کردن پیش‌نمایش"),
        ["UnmutePreview"] = ("Unmute preview", "فعال کردن صدای پیش‌نمایش"), ["Lossless"] = ("PNG is lossless", "PNG بدون افت کیفیت است"),
        ["BestImageQuality"] = ("Auto · maximum image quality", "خودکار · بیشترین کیفیت تصویر"),
        ["Busy"] = ("Please finish or cancel the current operation.", "ابتدا عملیات فعلی را تمام یا لغو کن."),
        ["SourceLimit"] = ("Trim points exceed the source duration.", "نقاط تریم خارج از مدت فایل اصلی‌اند."),
        ["TelegramError"] = ("Telegram video cannot exceed 3 seconds. Trim the timeline or choose Free export.", "مدت ویدیوی تلگرام نباید از ۳ ثانیه بیشتر باشد. تایم‌لاین را تریم کن یا خروجی آزاد را انتخاب کن."),
        ["SizeError"] = ("For Telegram, one side must be exactly 512 px and the other at most 512 px.", "برای تلگرام، یک ضلع باید دقیقاً ۵۱۲ و ضلع دیگر حداکثر ۵۱۲ پیکسل باشد."),
        ["FormatError"] = ("Use WEBM/VP9, PNG or WEBP for Telegram stickers, or switch to Free export.", "برای استیکر تلگرام WEBM/VP9، PNG یا WEBP انتخاب کن یا به حالت خروجی آزاد برو."),
    };
    public static string T(string key) => Words.TryGetValue(key, out var word) ? Language == "fa" ? word.Fa : word.En : key;
    public static void Apply(string language, bool light)
    {
        Language = language;
        foreach (var key in Words.Keys) Application.Current.Resources[key] = T(key);
        ThemePalette.Apply(light);
    }
}
