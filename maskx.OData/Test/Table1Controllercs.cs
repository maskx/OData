using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class Table1Controllercs : ODataController
    {
        public IActionResult Get()
        {
            return Ok();
        }
    }
}
