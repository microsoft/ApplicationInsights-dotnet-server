using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DemoLinuxWebApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        // GET api/values
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            for (int i = 0; i < 1000; i++)
            {
                var s = Guid.NewGuid();
            }

            for (int i = 0; i < 1000; i++)
            {
                try
                {
                    int ss = 10 / 0;
                }
                catch(Exception ex)
                {

                }
            }


            var currentProcess = Process.GetCurrentProcess();
            return new string[] { "value1", "value2",
                currentProcess.PrivateMemorySize64.ToString(),
                currentProcess.VirtualMemorySize64.ToString(),
                currentProcess.WorkingSet64.ToString()};
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
