using System;
using System.Collections.Generic;
using OpenTK;
using System.Linq;


namespace Demos
{
    static class UseCases
    {

        public static void Case1()
        {
            //var kernel = Interpreter.Instance;

            //var a = new Particle();
            //kernel.AwareOf("GodPush", typeof(NewtonPhysics).GetMethod("GodPush"), new Action<Particle,Vector3>(NewtonPhysics.GodPush), a);
            //kernel.Call("GodPush", Utils.MakeArgList("",a), new Vector3(1,0,0) );


            //Console.WriteLine(a.ToString());
        }

		//public static void BigCase1()
		//{
		//    var kernel = Cocktail.Kernel;
		//    var a = new Particle();

		//    kernel.AwareOf<Vector3>("GodPush", NewtonPhysics.GodPush, a);
		//    kernel.HappenToI("GodPush", Utils.MakeArgList("",a), new Vector3(1,0,0) );

		//    Console.WriteLine(a.ToString());
		//}

        public static void Case2()
        {
            //RigidBody a,b;
            //List<Point> SupportingPts = With(a,b).Collide(style);
        }
    }
}
