using System;
using System.ServiceModel;

namespace WcfHost
{
    internal class Program
    {
        static void Main()
        {
            Uri baseAddress = new Uri("http://localhost:8080/Service/");
            using (ServiceHost host = new ServiceHost(typeof(WcfService.Service), baseAddress))
            {
                try
                {
                    host.Open();
                    Console.ReadKey();

                    host.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);  
                    host.Abort();
                }
            }
        }
    }
}
