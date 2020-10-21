using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using cfg = soa.Entity.TCConfiguration;

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
            cfg.load();
            String Msg = "执行完成";
            String serverHost = cfg.get("ip");
            Teamcenter.ClientX.Session session = null;
            Teamcenter.ClientX.Session2 session2 = null;           
            try
            {
                session = new Teamcenter.ClientX.Session(serverHost);
                session2 = new Teamcenter.ClientX.Session2(serverHost);

                // Establish a session with the Teamcenter Server
                //Teamcenter.Soa.Client.Model.Strong.User user = session.login("infodba", "infodba", "", "", "", "SoaAppX");
                //Teamcenter.Soa.Client.Model.Strong.User user2 = session2.login("maxtt", "maxtt", "", "", "", "SoaAppX");
                Teamcenter.Soa.Client.Model.Strong.User user = session.login(cfg.get("dbname"), cfg.get("dbpassword"), "", "", "", "SoaAppX");
                Teamcenter.Soa.Client.Model.Strong.User user2 = session2.login(cfg.get("powerful_user_name"), cfg.get("powerful_user_password"), "", "", "", "SoaAppX");
                
                action.Invoke();
            }
            catch (Exception e)
            {
                Msg = e.ToString();
            }
            finally
            {
                if(null!=session)
                    session.logout();
                if (null != session2)
                    session2.logout();
            }

            return Msg;
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