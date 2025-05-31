# DataSerializationUtils

Библиотека для сериализации данных в JSON и YAML форматы.

## Возможности

### JSON сериализатор
- Поддержка примитивных типов данных
- Сериализация DateTime
- Работа с коллекциями
- Обработка специальных символов
- Защита от циклических ссылок

### YAML сериализатор
- Аналогичная функциональность с JSON
- Поддержка YAML-специфичного форматирования
- Корректная обработка отступов

## Установка

```shell
dotnet add package DataSerializationUtils
```

## Использование

```csharp
using DataSerializationUtils;

// Сериализация в JSON
var jsonSerializer = new JsonSerializer();
string json = jsonSerializer.Serialize(yourObject);

// Десериализация из JSON
var deserializedObject = jsonSerializer.Deserialize<YourType>(json);

// Сериализация в YAML
var yamlSerializer = new YamlSerializer();
string yaml = yamlSerializer.Serialize(yourObject);

// Десериализация из YAML
var deserializedFromYaml = yamlSerializer.Deserialize<YourType>(yaml);
```

## Лицензия

MIT
