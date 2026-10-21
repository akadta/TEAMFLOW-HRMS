using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hrms.HrmsCompanyApi.Controllers;

// Inherit from this for every company-app controller.
// Always filter queries with CurrentCompanyId so one company never sees another's data.
[ApiController]
[Authorize(AuthenticationSchemes = "CompanyBearer")]
public abstract class CompanyBaseController : BaseController
{
}