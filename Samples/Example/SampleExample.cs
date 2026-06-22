using UnityEngine;

namespace Splinemesherpro.Bovinelabstimelinedistance
{

    public class MyPublicSampleExampleClass
    {

        public void CountThingsAndDoStuffAndOutputIt()
        {
            var result = new MyPublicRuntimeExampleClass().CountThingsAndDoStuff(1, 2, false);
            Debug.Log("Call CountThingsAndDoStuffAndOutputIt returns " + result);
        }
    }
}
