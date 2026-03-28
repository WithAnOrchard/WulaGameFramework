using System.Collections.Generic;
using EssSystem.Core.AbstractClass;

namespace EssSystem.BackPackManager
{
    public class BackPackService :ServiceBase
    {
        public static BackPackService Instance => InstanceWithInit<BackPackService>(instance =>
        {
           
        });

    }
}