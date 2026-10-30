namespace Shared.Core.Contracts.Domain
{
    public interface INavigationService
    {
        /// <summary>
        /// <para>{RU} Собирает карту препятствий из json. Повторный вызов заменяет старый список.</para>
        /// <para>{EN} Builds the obstacle map from json. A repeat call replaces the previous list.</para>
        /// </summary>
        bool Initialize(string json);
    }
}
