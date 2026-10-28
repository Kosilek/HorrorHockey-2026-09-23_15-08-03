# FM.DI

**Версия 1.0.0**

## Краткое описание

Легковесный DI‑контейнер (Dependency Injection) для Unity/C# проектов.

### Реализовано

- **DIContainerService** — основной контейнер с поддержкой:
  - Singleton и Transient (жизненный цикл)
  - внедрения через конструктор (constructor injection)
  - именованных регистраций (`AddSingleton<T>(instance, name)` и т.п.)
  - фабрик для Transient‑сервисов
  - Fallback‑контейнера для цепочки разрешения

- **UnityInjectService** — внедрение в поля и свойства Unity‑компонентов через атрибут `[Inject]`.

- **Политики разрешения** (`ResolutionPolicy`): ThrowException, ReturnNull, TryGlobalFallback.

- **Атрибуты**:
  - `[Inject]` — поля и свойства
  - `[InjectConstructor]` — выбираемый конструктор для DI
  - `[InjectMethod]` — методы для внедрения после создания объекта
  - `[InjectName(name)]` — именованная зависимость в параметре конструктора

- **Интерфейсы**: `IServiceContainer`, `IRegistration`, `IResolver`, `IOptions<T>`, `IFactory`, `IDisposable`.

- **Factory&lt;T&gt;** — паттерн фабрики для transient‑сервисов.
