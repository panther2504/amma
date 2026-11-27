const en = {
  home: 'Home', baby: 'Baby', care: 'Care', learn: 'Learn', profile: 'Profile',
  gm: 'Good morning', ga: 'Good afternoon', ge: 'Good evening',
  weeks: 'weeks', tri: 'Trimester', sizeOf: 'Your baby is about the size of a', daysLeft: 'Days to go', dueDate: 'Due date', progress: 'Progress',
  quick: 'Quick access', kick: 'Kick count', foodGuide: 'Food guide', water: 'Water', meds: 'Medicines', ask: 'Ask Amma',
  todayMeds: "Today's medicines", hydration: 'Hydration', nextAppt: 'Next appointment', meals: "Today's meal plan", checklist: 'Daily checklist',
  tip: 'Tip of the day', warn: 'Know the warning signs', warnSub: 'Tap to see when to get help immediately', seeAll: 'See all',
  disclaimer: "Amma provides educational information only. Always follow your doctor's advice.",
}
type Key = keyof typeof en
const ta: Partial<Record<Key, string>> = {
  home: 'முகப்பு', baby: 'குழந்தை', care: 'பராமரிப்பு', learn: 'கற்றல்', profile: 'சுயவிவரம்',
  gm: 'காலை வணக்கம்', ga: 'மதிய வணக்கம்', ge: 'மாலை வணக்கம்',
  weeks: 'வாரங்கள்', tri: 'மும்மாதம்', sizeOf: 'உங்கள் குழந்தையின் அளவு', daysLeft: 'மீதமுள்ள நாட்கள்', dueDate: 'பிரசவ தேதி', progress: 'முன்னேற்றம்',
  quick: 'விரைவு அணுகல்', kick: 'அசைவு எண்ணிக்கை', foodGuide: 'உணவு வழிகாட்டி', water: 'தண்ணீர்', meds: 'மருந்துகள்', ask: 'அம்மாவிடம் கேளுங்கள்',
  todayMeds: 'இன்றைய மருந்துகள்', hydration: 'நீர்ச்சத்து', nextAppt: 'அடுத்த சந்திப்பு', meals: 'இன்றைய உணவுத் திட்டம்', checklist: 'இன்றைய பட்டியல்',
  tip: 'இன்றைய குறிப்பு', warn: 'அபாய அறிகுறிகளை அறியுங்கள்', warnSub: 'உடனடி உதவி எப்போது தேவை என அறிய தட்டவும்', seeAll: 'அனைத்தும்',
  disclaimer: 'அம்மா கல்வித் தகவல்களை மட்டுமே வழங்குகிறது. எப்போதும் உங்கள் மருத்துவரின் ஆலோசனையைப் பின்பற்றுங்கள்.',
}
export type Lang = 'en' | 'ta'
export const translate = (lang: Lang, k: Key) => (lang === 'ta' ? ta[k] : undefined) ?? en[k]
export type TKey = Key
