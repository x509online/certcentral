using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace certcentral.web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Cli ()
        {
            return View();
        }

        public ActionResult Search()
        {
            return View();
        }
        public ActionResult Devs()
        {
            return View();
        }
        public ActionResult Users()
        {
            return View();
        }
        public ActionResult Install()
        {
            return View();
        }

    }
}