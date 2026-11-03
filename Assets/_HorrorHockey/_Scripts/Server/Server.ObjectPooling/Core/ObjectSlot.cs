using Server.Shared.Configurations;
using System;

namespace Server.ObjectPooling.Core
{
    public class ObjectSlot<TObject> : IObjectSlot<TObject> where TObject : class, IPoolObj, new()
    {
        private readonly int _id;
        public int Id => _id;
        private bool _isUse;
        public bool IsUse => _isUse;
        public TObject TObj { get; set; }

        public ObjectSlot(int id)
        {
            _id = id;
            TObj = new();
            _isUse = false;
        }

        public void SetObject(TObject obj)
        {
            _isUse = true;
            TObj.SetObject(obj);
        }

        public void ResetObject()
        {
            _isUse = false;
            TObj.ResetObject();
        }

        public void UpdateData()
        {
            throw new NotImplementedException();
        }
    }
}