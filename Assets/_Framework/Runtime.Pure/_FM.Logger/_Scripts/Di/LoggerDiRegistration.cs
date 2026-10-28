using FM.General.Core.Interfaces.Contracts;

namespace _FM.Logger
{
    /// <summary>
    /// {RU} Регистрация Logger в DI. Обнаруживается DiBootstrap через IRegisterInDI. Без зависимости от _FM.DI.
    /// <para>{EN} Logger registration in DI. Discovered by DiBootstrap via IRegisterInDI.</para>
    /// </summary>
    public class LoggerDiRegistration : IRegisterInDI
    {
        public void AddRegistrations(IRegistrationContext context)
        {
            context.RegisterSingleton<ILogger, Logger>();
        }
    }
}
