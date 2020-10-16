using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace soa.Controllers
{
    public class ItemController : Controller
    {
        public ContentResult Index(String id)
        {          
            return Content(id + "connect successful.");
        }

        [HttpPost]
        public ContentResult creteOrUpdateItem()
        {
            var item = JObject.Parse(GetPostContent()).ToObject<Entity.Item>();
            //AOP封装
            var result = Handle(() => 
            new DataManagement().createTCItem("Item", item.number, item.name, item.detail, item.unit, item.proType, item.reqName)
            );
            return Content(result);
        }

        [HttpGet]
        public ContentResult deleteItem(String id)
        {
            //AOP封装
            var result = Handle(() =>
                   new DataManagement().deleteItem(id)
            );
            return Content(result);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public String Handle(Action action)
        {
            String Msg = "执行完成";
            String serverHost = "http://192.168.110.131:7001/tc";
            try
            {

                Teamcenter.ClientX.Session session = new Teamcenter.ClientX.Session(serverHost);
                Teamcenter.ClientX.Session2 session2 = new Teamcenter.ClientX.Session2(serverHost);

                // Establish a session with the Teamcenter Server
                Teamcenter.Soa.Client.Model.Strong.User user = session.login("infodba", "infodba", "", "", "", "SoaAppX");
                Teamcenter.Soa.Client.Model.Strong.User user2 = session2.login("maxtt", "maxtt", "", "", "", "SoaAppX");

                action.Invoke();

                session.logout();
                session2.logout();
            }
            catch (Exception e)
            {
                return e.ToString();
            }

            return "执行完成";
        }

        public String GetPostContent()
        {
            //获取JSON         
            var stream = new MemoryStream();
            Request.Body.CopyTo(stream);

            string json = string.Empty;
            string responseJson = string.Empty;
            using (System.IO.StreamReader reader = new System.IO.StreamReader(stream))
            {
                stream.Seek(0, System.IO.SeekOrigin.Begin);
                json = reader.ReadToEnd();
            }
            return json;
        }

    }
}