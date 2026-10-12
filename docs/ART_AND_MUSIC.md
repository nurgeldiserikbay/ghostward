# Картинки и музыка сущностей — промпты для генерации

Игра сама подхватывает файлы, если они лежат в нужной папке под нужным именем. Если файла нет, рисуется процедурный силуэт и играет синтезированный мотив. Поэтому добавлять можно постепенно, по одному.

## Картинки (Midjourney, DALL·E, Leonardo и т. п.)

**Куда класть:** `Assets/Resources/Entities/<Имя>.png`, имя — из таблицы ниже (например, `Assets/Resources/Entities/Banshee.png`).

**Требования к файлу:**
- вертикально 4:5, лучше 1024×1280;
- фигура целиком, от макушки до ступней, по центру, ступни у нижнего края;
- **лучше всего — PNG с прозрачным фоном**: сгенерируйте на чёрном фоне, потом уберите фон (remove.bg, Photoshop, Canva) и сохраните PNG с прозрачностью. Игра это видит и ничего не вырезает;
- если прозрачности нет, игра вырезает только почти чистый чёрный (#000). Тогда фон должен быть ровно чёрным, а тёмная одежда на картинке — не чисто чёрной, иначе она станет дырявой;
- без текста, рамок и водяных знаков;
- одно существо на картинке.

**Общая часть промпта** (подставлять перед описанием каждой сущности):

```
photorealistic horror film still, full-body shot, single figure standing facing the camera,
centered, entire body visible from head to feet, isolated on a pure black background,
harsh cold rim light from behind, deep shadows, desaturated colors, 35mm film grain,
practical makeup effects, unsettling, realistic proportions, vertical 4:5
```

**Негативный промпт** (если сервис поддерживает):

```
cartoon, anime, illustration, 3d render, cute, smiling friendly, text, watermark, logo,
frame, border, multiple characters, cropped feet, cropped head, background scenery, daylight
```

В Midjourney добавить в конец: `--ar 4:5 --style raw --no text, watermark, background`.

Не используйте в промптах названия фильмов, сериалов и имена актёров: картинка должна быть оригинальной, иначе её нельзя ставить в игру.

| Файл | Сущность | Описание (после общей части) |
|---|---|---|
| Ghost | Призрак | a gaunt woman's ghost in a tattered grey burial shroud, hood shadowing a hollow face, black tears streaming from empty eyes, mouth stretched in a silent scream, long thin arms reaching forward with clawed fingers |
| Shade | Тень | a tall faceless shadow person made of pure darkness, impossibly long arms hanging to the knees, clawed fingers, two faint red pinpoint eyes, edges smoking into the dark |
| Albasty | Албасты | a Central Asian woman in a long pale dress, waist-length black tangled hair covering her face except one glinting yellow eye, feet twisted backwards, long clawed hands |
| Poltergeist | Полтергейст | a formless spiky mass of shadow and floating broken debris, crockery shards and splinters, many glinting eyes and a wide toothed maw in the middle |
| Double | Двойник | a pitch-black human silhouette shaped exactly like an ordinary man, with a huge unnatural white grin full of teeth and small white eyes |
| Kikimora | Кикимора | a tiny emaciated swamp hag crawling spider-like on four long jointed limbs, wild matted hair, huge staring eyes, needle teeth, grey-green skin |
| Upyr | Упырь | a hunched Slavic vampire corpse, grey rotting skin over visible ribs, head thrust forward, gaping mouth with long fangs, bloodshot red eyes, long clawed hands |
| Mara | Мара | a hunched old hag in black rags, floor-length grey hair, faint violet glowing eyes, long bony fingers hanging down to the floor |
| Banshee | Банши | an Irish banshee, a gaunt woman in a torn grey gown, long white hair blown upward as if in a storm, mouth open in a terrible wail, red-rimmed eyes, arms raised |
| Domovoi | Домовой | a small shaggy old house spirit, bearded from head to floor, covered in coarse brown hair, glowing amber coal-like eyes under bushy brows, long arms with clawed hands |
| Changeling | Подменыш | a wizened changeling infant standing upright, oversized wrinkled old-man head on a tiny body, huge glossy black eyes, too-wide grin with tiny sharp teeth, thin long arms |
| BlackDog | Чёрный пёс | a monstrous black hound the size of a calf, front view, shaggy coat like smoke, glowing orange-red eyes, bared fangs, low growling stance |
| Rusalka | Русалка | a drowned girl in a soaked white shift, long wet black hair hanging to her knees, pale greenish skin, black eyes, water dripping from her fingers |
| Zheztyrnak | Жезтырнак | an eerie beautiful Kazakh steppe woman in a long traditional dress, two long black braids, pale face with narrow glowing eyes, enormous long copper claws instead of fingernails |
| Llorona | Ла Йорона | a weeping woman in a long white wedding dress and veil, hands pressed to her face, dark tears running down, hollow eyes visible between the fingers |
| Ghoul | Гуль | an emaciated desert ghoul crawling on all fours, hunched spine rising above its head, ash-grey skin, glowing yellow eyes, a wide mouth of jagged teeth stretching across its face |
| Kitsune | Кицунэ | a nine-tailed fox spirit, a slender dark fox with a white porcelain-like face and narrow glowing blue eyes, nine tails fanned out behind it, pale blue foxfire flames floating around |
| CorpoSeco | Корпу-Секу | a dried mummified corpse standing stiffly, skin like cracked leather stretched over bones, visible ribs, empty eye sockets with tiny pale pinpoints of light, jaw hanging open |
| MystanKempir | Мыстан-кемпир | a hunchbacked Kazakh witch crone in a dark headscarf, hooked nose, one long protruding tooth, glowing green eyes, leaning on a crooked staff, clawed hand reaching out |
| Jinn | Джинн | a jinn made of smoke and fire, muscular torso rising out of a swirling smoke tornado instead of legs, burning orange eyes and mouth, arms raised |
| Lechuza | Ла Лечуса | a giant barn owl the size of a woman with a pale human woman's face, dark wings spread wide, sharp talons, glowing amber eyes |
| Pontianak | Понтианак | a pale woman in a long white dress, very long straight black hair, red eyes, long fingernails, floating slightly above the ground |
| BabaYaga | Баба-яга | an ancient bony crone with iron teeth and a long hooked nose, standing in a wooden mortar holding a pestle, tattered peasant clothes, wild grey hair |
| Shurale | Шурале | a very tall thin hairy forest creature with a single horn on its forehead, extremely long branch-like fingers, a wide grin, amber eyes |
| Cucuy | Эль Кукуй | a shapeless mass of living darkness rising from the floor, long thin arms reaching forward, several glowing red eyes, the suggestion of a hunched figure |
| Kuchisake | Кутисакэ-онна | a tall woman in a long beige trench coat, long black hair, her mouth slit from ear to ear in a horrible grin, holding large scissors |
| BloodyMary | Кровавая Мэри | a woman with a bloodied face and dark hair standing inside an ornate oval mirror frame, her hands pressed against the glass from the inside |
| Ubyr | Убыр | a ball of fire with a screaming old witch's face inside it, trailing sparks and smoke like a comet |
| Chupacabra | Чупакабра | a hairless grey reptilian dog-like creature crouching, a row of spines along its back, large red eyes, fangs, clawed hands |
| Jiangshi | Цзянши | a stiff hopping corpse in an old Chinese official's robe and hat, arms stretched straight forward, a yellow paper talisman on its forehead, greenish-grey skin |
| Strigoi | Стригой | a tall gaunt undead man with red hair and pale blue eyes, high-collared dark cloak, long fingers, pointed teeth |
| Karakoncolos | Каракончолос | a huge hairy winter creature covered in frost, small horns, long arms reaching to the ground, icy glowing blue eyes |
| Tunda | Ла Тунда | a hunched swamp shapeshifter woman, one normal leg and one wooden peg leg, long hair, face in shadow with faintly glowing eyes, one hand beckoning |
| Aswang | Асванг | a crouching half-woman half-bat creature, leathery wings stretched from the arms, a long thin tongue hanging from its mouth, orange eyes |
| Vodyanoy | Водяной | a fat old water spirit with a frog-like face, a beard of green river weed, webbed hands, bulging eyes, dripping wet |
| Abaasy | Абаасы | a demon with one eye in the middle of its forehead, one arm growing from its chest, a single leg, iron teeth, stone-grey skin |
| MulaSemCabeca | Мула-сем-Кабеса | a black mule seen from the side with no head, flames erupting from its neck, iron horseshoes striking sparks |
| Penanggalan | Пенанггалан | a woman's floating head with long hair, her entrails and stomach dangling beneath it, glistening, eyes glaring |
| BellWitch | Ведьма Белла | a translucent spectral old woman in a 19th-century bonnet and shawl, mouth open as if singing a hymn, one hand raised to slap |
| ZhalmauyzKempir | Жалмауыз-кемпир | a cannibal crone with seven heads on long necks fanning out from her shoulders, each with glowing eyes and a gaping mouth, dark robe |

Что игра делает с картинкой сама: вырезает чёрный фон, тонирует под холодную плёнку, добавляет зерно, мерцание, разрывы строк и цветовой сдвиг на сбоях, лёгкое «дыхание» фигуры, рассыпание при изгнании. Та же картинка идёт в скример на весь экран, в дневник и в «отражение» фирменного испуга. В дневнике до первой встречи показывается только тёмный силуэт.

## Музыка (Suno)

**Куда класть:** `Assets/Resources/Music/<Имя>.ogg` (подойдут и `.mp3`, `.wav`), имена те же, что у картинок.

**Важно про права:** для платной игры с рекламой треки нужно генерировать на платном тарифе Suno (Pro или Premier). Треки с бесплатного тарифа ставить в игру нельзя, это коммерческое использование.

**Как генерировать:** режим Custom, включить **Instrumental**, в поле Style вставить строку из таблицы. Нужна 1–2 минуты тихой атмосферы, а не песня. Если в начале или в конце трека громкий удар, обрежьте его: трек играет по кругу всю ночь. Громкость игра выставляет сама. Трек звучит тихо под расследованием и почти исчезает на охоте.

Общий хвост для каждого стиля: `dark ambient horror, slow, sparse, quiet, lots of silence, reverb, no drums, no vocals, loopable`

| Файл | Style (перед общим хвостом) |
|---|---|
| Ghost | detuned antique music box lullaby, A minor, 60 bpm, tape warble |
| Shade | low bowed cello drones, locrian, 48 bpm, subsonic rumble |
| Albasty | Kazakh dombra plucks, minor pentatonic, 66 bpm, steppe wind |
| Poltergeist | out-of-tune toy piano, whole-tone scale, 72 bpm, creaking house |
| Double | reversed music box, locrian, 54 bpm, unsettling mirror ambience |
| Kikimora | breathy wooden flute, phrygian, 70 bpm, swamp night insects |
| Upyr | low cello, phrygian, 50 bpm, grave dirt textures |
| Mara | wordless female choir humming, dorian, 44 bpm, sleep paralysis drone |
| Banshee | distant keening female choir, Irish dorian, 50 bpm, cold wind |
| Domovoi | old harmonium, minor, 58 bpm, creaking wooden house |
| Changeling | slow music box, dorian, 64 bpm, distant baby crying muffled |
| BlackDog | deep church bell tolls, locrian, 40 bpm, foggy moor |
| Rusalka | ethereal female choir, minor, 52 bpm, dripping water, river |
| Zheztyrnak | dombra with hijaz scale, 70 bpm, metallic scrapes |
| Llorona | lonely Spanish nylon guitar, minor, 56 bpm, distant sobbing |
| Ghoul | Middle Eastern ney flute, hijaz, 52 bpm, desert night wind |
| Kitsune | shakuhachi flute, hirajoshi scale, 48 bpm, temple silence |
| CorpoSeco | Brazilian viola caipira plucks, phrygian, 62 bpm, dry rattling |
| MystanKempir | Kazakh kobyz bowed drone, pentatonic, 46 bpm, muttering whispers |
| Jinn | theremin, hijaz, 50 bpm, swirling sandstorm |
| Lechuza | haunting flute, minor, 58 bpm, owl calls at night |
| Pontianak | Indonesian gamelan, pelog scale, 50 bpm, frangipani night jungle |
| BabaYaga | wheezing harmonium, phrygian, 64 bpm, dark Slavic forest |
| Shurale | Bashkir kurai flute, pentatonic, 72 bpm, creaking forest |
| Cucuy | slow music box, whole-tone, 50 bpm, scratching under the bed |
| Kuchisake | koto plucks, hirajoshi scale, 54 bpm, scissors snipping |
| BloodyMary | detuned upright piano, minor, 52 bpm, whispered chanting |
| Ubyr | theremin, pentatonic, 46 bpm, crackling fire |
| Chupacabra | Mexican guitar plucks, phrygian, 76 bpm, night desert insects |
| Jiangshi | Chinese temple bells, pentatonic, 60 bpm, hollow wooden knocks |
| Strigoi | church organ, Romanian minor scale, 52 bpm, gothic crypt |
| Karakoncolos | low male choir, locrian, 40 bpm, howling blizzard |
| Tunda | breathy pan flute, dorian, 56 bpm, mangrove swamp at night |
| Aswang | gamelan, pelog, 66 bpm, clicking sounds |
| Vodyanoy | deep cello, minor, 44 bpm, underwater bubbles |
| Abaasy | Yakut khomus jaw harp drone, pentatonic, 42 bpm, frozen taiga |
| MulaSemCabeca | Brazilian guitar plucks, phrygian, 80 bpm, distant galloping hooves |
| Penanggalan | gamelan, pelog, 46 bpm, wet dripping |
| BellWitch | 19th century hymn on reed organ, dorian, 50 bpm, old farmhouse |
| ZhalmauyzKempir | low throat-singing choir, pentatonic, 46 bpm, many whispering voices |

После добавления файлов игру нужно пересобрать: файлы в `Resources` попадают в APK при сборке.
