using ContractsServer.Core.Enum;
using ContractsServer.Data.Entity;
using FM.Mathematics.DataTypes;

namespace ContractsServer.Core.Interfaces
{
    public interface IEntityProviderService
    {
        EntityDataUnit GetDataUnit(Byte2 positionUnit);
        EntityDataUnit GetDataUnit(TypeEntityUnit type);
    }
}