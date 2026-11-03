namespace Server.Shared.Configurations
{ 
    public interface IPoolObj
    {
        void SetObject<TData>(TData data);
        void ResetObject();
        void UpdateData();
    }
}