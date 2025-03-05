using DocShare.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DocShare.Pages;

public class IndexModel : PageModel
{
    public IndexModel(ILogger<IndexModel> logger, IWebHostEnvironment env)
    {
    }

    public void OnGet()
    {
    }
}
