# Ghost Ward — шаги в Google Play Console

Всё, что нужно загрузить и вставить, собрано здесь. Идите по порядку.

## Файлы

| Что | Где |
|---|---|
| Сборка для закрытого теста (тестовая реклама Google) | `Builds/GhostWard-testing.aab`, versionCode 1, версия 1.0 |
| Символы для отчётов о сбоях | `Builds/GhostWard-testing-1.0-v1-IL2CPP.symbols.zip` |
| Иконка 512×512 | `Builds/store-art/icon-512.png` |
| Баннер 1024×500 (Feature graphic) | `Builds/store-art/feature-graphic-1024x500.jpg` |
| Скриншоты телефона 1080×1920 с подписями | `Builds/store-art/screenshot-1.jpg` … (после шага «Скриншоты») |

Пересобрать:
- AAB — Unity, меню **GhostWard → Собрать AAB — тестирование (тестовая реклама)**. Ключ загрузки TheLightCome берётся из `D:\WORK\Passwords\thelightcome\release.properties`. Перед каждой новой загрузкой увеличить Bundle Version Code.
- Картинки — `powershell -ExecutionPolicy Bypass -File tools/StoreArtCompose.ps1`.

### Скриншоты

Нужны снимки с телефона (кнопки питания и громкости вниз). Положите 4–6 штук в `Builds/store-art/raw`, назвав их по порядку (`1.png`, `2.png` …), и запустите `tools/StoreArtCompose.ps1`: он подгонит их под 1080×1920 и добавит подписи сверху.

| № | Что снять | Подпись |
|---|---|---|
| 1 | Сущность в комнате, крупно | Something is in your room |
| 2 | Открытый прибор: ЭМП, тепловизор или ночная камера | Find the clues |
| 3 | Дневник со статьёй о сущности | Name the entity |
| 4 | Защита: соль, круг, свеча или предмет в руке | Drive it away before dawn |
| 5 | Бестиарий / список сущностей | Keep a journal |
| 6 | Главное меню | Play in the dark |

Google требует минимум 2 скриншота. Снимайте в тёмной комнате: так видно атмосферу.

## 1. Создать приложение

All apps → Create app:
- App name: **Ghost Ward**
- Default language: **English (United States) – en-US**
- App or game: **Game**; Free or paid: **Free**
- Отметить согласия (Developer Program Policies, US export laws).
- Имя пакета **`com.thelightcome.ghostward`** Play возьмёт из первого загруженного AAB; потом его не поменять.

## 2. Store listing (Grow → Store presence → Main store listing)

- **App name:** Ghost Ward
- **Short description** (до 80 символов):

  ```
  Hunt the spirits haunting your room: find clues, name them, survive till dawn.
  ```

- **Full description:**

  ```
  Something has moved into your room.

  Ghost Ward turns your own home into a haunted house. Look around through your phone camera: an entity has settled somewhere in the dark, and it is watching you. Find out who it is before it finds you.

  • Investigate with real ghost-hunting tools: an EMF reader, a thermal camera, night vision and a spirit box that answers when you speak out loud
  • 40 entities from folklore around the world: the Albasty, the Domovoi, the Banshee, La Llorona, Kitsune, Baba Yaga and many more
  • Each one has its own clues, habits and lair, and fears only one thing: salt, iron, a bell, a mirror, wormwood or light
  • Name the entity correctly, protect yourself with salt circles, candles and amulets, and survive until dawn
  • Your journal keeps every clue, story and creature you have identified
  • Hunts, whispers, knocks and a radio that should not be talking
  • Take photos of what you saw and share them

  Best played at night, with the lights off and headphones on.

  The game uses the camera to show your room and the microphone to hear how loud it is around you. Both are processed only on your device; nothing is recorded or uploaded.

  Ghost Ward is a horror game for teens and adults. It contains ads.
  ```

- **App icon:** `icon-512.png`; **Feature graphic:** `feature-graphic-1024x500.jpg`
- **Phone screenshots:** `screenshot-1.jpg` …, по порядку.
- Store settings: Category **Games → Adventure**; Tags: Horror, Adventure, Investigation; Contact email: ваш gmail; Website можно оставить пустым.
- Нигде в карточке не используйте слово «Supernatural» (товарный знак сериала).

### Перевод на русский (необязательно, Translations → Add your own translation → Russian)

- Short description:

  ```
  Духи в твоей комнате: найди улики, узнай, кто это, и доживи до рассвета.
  ```

- Full description:

  ```
  В твоей комнате кто-то поселился.

  Ghost Ward превращает твой собственный дом в дом с привидениями. Осмотрись через камеру телефона: где-то в темноте устроилась сущность, и она наблюдает за тобой. Узнай, кто это, раньше, чем она доберётся до тебя.

  • Настоящие приборы охотника за привидениями: ЭМП-детектор, тепловизор, ночная камера и радио, которое отвечает, когда ты говоришь вслух
  • 40 сущностей из фольклора разных народов: албасты, домовой, банши, Ла Йорона, кицунэ, Баба-яга и многие другие
  • У каждой свои улики, повадки и логово, и боится она только одного: соли, железа, колокольчика, зеркала, полыни или света
  • Назови сущность правильно, защитись кругом из соли, свечами и амулетами и доживи до рассвета
  • Дневник хранит все улики, истории и узнанных существ
  • Охота, шёпот, стуки и радио, которое не должно разговаривать
  • Фотографируй то, что увидел, и делись снимками

  Лучше играть ночью, при выключенном свете и в наушниках.

  Игра использует камеру, чтобы показать твою комнату, и микрофон, чтобы слышать, насколько вокруг шумно. И то и другое обрабатывается только на устройстве: ничего не записывается и не отправляется.

  Ghost Ward — хоррор для подростков и взрослых. В игре есть реклама.
  ```

## 3. App content (Policy → App content)

| Раздел | Ответ |
|---|---|
| Privacy policy | `https://docs.google.com/document/d/e/2PACX-1vQA_3QsTLu4_N_SarT-LKJo2j7SOflbBFYWf8gWR-DKmo4TIgpZUr9vLrAJEOoPMGNbnWV3Hrbuos4C/pub` |
| App access | All functionality is available without special access |
| Ads | **Yes, my app contains ads** |
| Advertising ID | **Yes** → цели: **Advertising or marketing**, **Analytics** (AdMob) |
| Content rating | Анкета IARC, категория Game: **страх / пугающие сцены — да** (хоррор, внезапные появления существ); насилие — только фэнтезийное, без крови и без насилия над людьми; азартных игр, общения игроков, обмена данными с другими пользователями и покупок нет. Ожидается PEGI 12 / Teen |
| Target audience and content | Возраст: **13–15, 16–17, 18+**. Младше 13 не отмечать: игра не для детей и не в программе Families |
| Appeals to children | **Нет** |
| News app / COVID / Government / Financial / Health | Нет |
| Data safety | См. ниже |

### Data safety

- Does your app collect or share user data? **Yes**
- All data encrypted in transit? **Yes**
- Way to request deletion? **No**: аккаунтов нет, данные игры только на устройстве и удаляются вместе с приложением.
- Собираемые данные (все: **Collected, Shared**, обязательные — **required**, цели: **Advertising or marketing, Analytics, Fraud prevention, security and compliance**):
  - **Location → Approximate location** (IP-адрес, через AdMob)
  - **App activity → App interactions** (просмотры и нажатия рекламы, AdMob)
  - **App info and performance → Diagnostics** (AdMob)
  - **Device or other IDs** (Advertising ID и App Set ID, AdMob)
- **Не отмечать:** камеру, микрофон, фото и аудио. Кадры камеры и уровень звука обрабатываются только на устройстве и никуда не уходят; фото сохраняется в галерею только по кнопке «Поделиться».

## 4. Закрытое тестирование (Test and release → Testing → Closed testing)

1. Create track (или «Closed testing – Alpha»).
2. Testers: список email минимум из **12 человек**; сохраните ссылку для участия и разошлите её.
3. Countries: выберите страны.
4. Create release → загрузите `GhostWard-testing.aab`. При первой загрузке согласитесь на **Play App Signing** (Google хранит ключ подписи приложения, ключ TheLightCome становится ключом загрузки — тот же, что у Fruit Tree).
5. После загрузки в **App bundle explorer** → версия 1 → **Downloads → Native debug symbols → Upload** `GhostWard-testing-1.0-v1-IL2CPP.symbols.zip`. Предупреждение про deobfuscation file можно игнорировать.
6. Release name: `1.0 (1) test ads`; Release notes:

   ```
   <en-US>
   First test build. Ads in this build are Google test ads.
   </en-US>
   ```

7. Review release → Start rollout. Тест должен идти **14 дней** с 12+ участниками, которые действительно играют; затем откроется заявка на продакшн. Google спросит, что вы улучшили по отзывам, — записывайте их.

Тестерам стоит сказать: играть вечером в комнате, разрешить камеру и микрофон; в настройках есть режим AR (экспериментальный).

## 5. AdMob

- Privacy & messaging: сообщение **GDPR** и **US states** для Ghost Ward со ссылкой на политику выше. Без него в Европе почти не будет рекламы.
- Blocking controls: Max ad content rating **T**; заблокированы Dating, References to Sex, Sexual & Reproductive Health, Social Casino Games (сделано 2026-11-27).

## 6. После закрытого теста — продакшн

1. Скажите мне — я соберу AAB **продакшн (настоящая реклама)** с **versionCode 2** и новым symbols.zip.
2. Apply for production → загрузите его в Production, символы — в App bundle explorer.
3. В AdMob после публикации свяжите приложение со страницей в Google Play (Apps → App settings → Add app store details).
4. Перед продакшном заменить звуки из `Assets/Resources/Sfx` (сделаны на бесплатном тарифе ElevenLabs).
