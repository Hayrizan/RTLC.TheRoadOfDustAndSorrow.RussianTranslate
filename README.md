# The Road of Dust and Sorrow Demo — русификатор

Русский язык для Steam-демо 0.5.42 на Windows x64. Переведены интерфейс, диалоги, записки, инвентарь, цели и подсказки.

## Установка

1. Скачайте ZIP со страницы [релиза](https://github.com/Hayrizan/RTLC.TheRoadOfDustAndSorrow.RussianTranslate/releases/latest). В архив уже входит BepInEx 5.4.23.6.
2. Закройте игру и распакуйте архив в папку с `rds.exe`. При обновлении со старой версии перенесите папку `BepInEx/plugins/RussianTranslate` за пределы `BepInEx/plugins`.
3. Запустите демо через Steam. При первом запуске с плагином включится русский. Язык можно изменить в настройках.

Чтобы убрать русификатор, перенесите `BepInEx/plugins/RTLC.TheRoadOfDustAndSorrow.RussianTranslate` за пределы `BepInEx/plugins`. Журнал загрузки находится в `BepInEx/LogOutput.log`.

## Сборка

Нужны .NET SDK, установленное демо и [BepInEx 5.4.23.6 для Windows x64](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6).

```powershell
dotnet build -c Release "-p:GameDir=D:\Games\The Road of Dust and Sorrow Demo"
```

Если загрузчик находится отдельно, укажите `-p:BepInExCore=D:\Games\BepInEx\core`. Значения `RUS` в `Russian.json` можно редактировать. Ключи вычисляются как Base64(SHA-256) от английского текста в UTF-8 с переводами строк LF.

Плагин и перевод подготовлены с помощью Codex.
