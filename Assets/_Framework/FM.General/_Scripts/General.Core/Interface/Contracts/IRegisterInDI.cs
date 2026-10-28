namespace FM.General.Core.Interfaces.Contracts
{
    /// <summary>
    /// {RU} Регистрация сервисов модуля в DI. Реализуют Logger, Config. DiBootstrap обнаруживает через Reflection.
    /// Модуль ссылается только на FM.General — без зависимости от _FM.DI.
    /// <para>{EN} Module registration in DI. Implemented by Logger, Config. DiBootstrap discovers via Reflection.</para>
    /// </summary>
    public interface IRegisterInDI
    {
        void AddRegistrations(IRegistrationContext context);
    }
}
