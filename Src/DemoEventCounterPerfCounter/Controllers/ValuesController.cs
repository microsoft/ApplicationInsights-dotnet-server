using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DemoEventCounterPerfCounter.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        // GET api/values
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            for( int i = 0; i< 1000; i++)
            {
                var guid = Guid.NewGuid();
            }

            int[] nums = new int[10];
            for( int i=0;i<10;i++)
            {
                nums[i] = i * 100;
            }

            Parallel.ForEach(nums, (num) =>
            {
                num++;
            });

            return new string[] { "value1", "value2", "OS: "+ RuntimeInformation.OSDescription, "" + "Framework" + RuntimeInformation.FrameworkDescription };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            GC.Collect();
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
