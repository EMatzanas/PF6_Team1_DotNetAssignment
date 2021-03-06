using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PF6_Team1_DotNetAssignment.Database;
using PF6_Team1_DotNetAssignment.Models;
using PF6_Team1_DotNetAssignment.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PF6_Team1_DotNetAssignment.Services.Implementations
{
    public class ProjectService : IProjectService
    {
        private readonly Team1DbContext _context;
        private readonly ILogger<ProjectService> _logger;
        private readonly IUserService _userService;

        //private readonly IProductValidation _productValidator;

        public ProjectService (Team1DbContext context, ILogger<ProjectService> logger, IUserService userService)
        {
            _context = context;
            _logger = logger;
            _userService = userService;
        }
        public async Task<Project> CreateProjectAsync(ProjectOption options)
        {
            // Validations.........................
            if (options == null)
            {
                _logger.LogError("Null options");
                return null;
            }
            if (options.Title == null)
            {
                _logger.LogError("Please specify a Title");
                return null;
            }
            if (options.RequiredFunds <= 0)
            {
                _logger.LogError("Please specify the required funds for your project");
                return null;
            }
            if (options.Deadline == default)
            {
                _logger.LogError("Please specify a correct deadline for your project");
                return null;
            }

            // Find the project with the same title..................... 
            var ProjectWithSameTitle = await _context.Projects.SingleOrDefaultAsync(proj => proj.Title == options.Title);
            if (ProjectWithSameTitle != null)
            {
                _logger.LogError($" Project with title {options.Title} already exists");
                return null;
            }

            // Create the new project 
            var newProject = new Project
            {
                Title = options.Title,                              // Mapping 
                Description = options.Description,
                Category = options.Category,
                Country = options.Country,
                FileName = options.FileName,
                RequiredFunds = options.RequiredFunds,
                CurrentFunds = 0, 
                CreatedDate = DateTime.Now,                         // ????????  
                Deadline = options.Deadline,

               UserId=options.UserId
            };

            // Save and Update Db
            await _context.Projects.AddAsync(newProject);
            await _context.SaveChangesAsync();
            return newProject;
        }

        public async Task<int> DeleteProjectByIdAsync(int id)           // ??????????????????
        {
            //Validation..................
            if (id < 0)
            {
                _logger.LogError("Id invalid! Id cannot be equal or less than zero");
                return -1;
            }

            // Find Project
            var ProjectToDelete = await GetProjectByIdAsync(id);        //Reusability
            if (ProjectToDelete == null)
            {
                return -1;
            }

            //Delete Project 
            _context.Projects.Remove(ProjectToDelete);
            // Update DB
            return await _context.SaveChangesAsync();
        }

        public async Task<Project> GetProjectByIdAsync(int id)
        {
            //Validation..................
            if (id < 0)
            {
                _logger.LogError("Id invalid! Id cannot be equal or less than zero");
                return null;
            }
            //Find the project
            var ProjectToBeRead = await _context.Projects.SingleOrDefaultAsync(p => p.ProjectId == id);
            return ProjectToBeRead;

        }

        public async Task<List<Project>> GetProjectsAsync()
        {  
          return   await _context.Projects.Include(project => project.MyPackages).ToListAsync(); 
        }

        public async Task<Project> UpdateProjectById(int id, ProjectOption projectOption)
        {
            var ProjectToUpdate = await GetProjectByIdAsync(id);        //Reusability
            if (ProjectToUpdate == null)
            {
                return null;
            }
            
            // Update the project
            ProjectToUpdate.Title = projectOption.Title;                                // Mapping 
            ProjectToUpdate.Description = projectOption.Description;
            ProjectToUpdate.Category = projectOption.Category;
            ProjectToUpdate.Country = projectOption.Country;
            //ProjectToUpdate.MyImage = projectOption.MyImage;
            ProjectToUpdate.RequiredFunds = projectOption.RequiredFunds;
            ProjectToUpdate.CurrentFunds = projectOption.CurrentFunds;
            ProjectToUpdate.CreatedDate = projectOption.CreatedDate;
            ProjectToUpdate.Deadline = projectOption.Deadline;
            
            // Save and Update Db
            await _context.SaveChangesAsync();
            return ProjectToUpdate;
        }

        //Get the project's current % progress
        public async Task<float> GetCurrentProgressAsync(ProjectOption projectOption)
        {
            if (projectOption.ProjectId < 0)
            {
                _logger.LogError($"The project '{projectOption.Title}' does not exist!");
                return default;
            }

            await _context.Projects.SingleOrDefaultAsync(proj => proj.ProjectId == projectOption.ProjectId);
            return (projectOption.CurrentFunds / projectOption.RequiredFunds) * 100;
        }





        public async Task<List<PackageOption>> GetPackagesByIdAsync(int id)
        {
            var project = await GetProjectByIdAsync(id);

            var new_package_list = new List<PackageOption>();

            foreach (var Package in project.MyPackages)
            {
                new_package_list.Add(new PackageOption
                {
                    PackageId = Package.PackageId,
                    Title = Package.Title,
                    Price = Package.Price,
                    Description = Package.Description,
                    Reward = Package.Reward,
                    ProjectId = Package.ProjectId
                });
            }
            return new_package_list;

        }

        public async Task<bool> AppendPackageToProjectAsync(int id)
        {
            if (id < 0)
            {
                _logger.LogError("Id is invalid");
                return false;
            }

            var project = await GetProjectByIdAsync(id);

            var package_list = await _context.Packages.Where(pack => pack.ProjectId == id).ToListAsync();
            if (package_list == null)
            {
                _logger.LogError("List is empty or Project has no Packages");
                return false;
            }

            return true;
        }

        public async Task<bool> UpdateCurrentFunds(int id, int userId)
        {
            var package = await _context.Packages.SingleOrDefaultAsync(pack => pack.PackageId == id);
            var project = await GetProjectByIdAsync(package.ProjectId);
            project.CurrentFunds += package.Price;
            await _context.SaveChangesAsync();
            // Update User Backer List:
            var user = await _userService.GetUserByIdAsync(userId);
          
            var projectuserbacker = new ProjectUserBacker
            {
                ProjectKey = project.ProjectId,
                UserKey = userId
            };

             if (await _context.ProjectUserBackers
                .Where(p => p.ProjectKey == projectuserbacker.ProjectKey && p.UserKey == projectuserbacker.UserKey)
                .SingleOrDefaultAsync() == null)
            {
                user.BackedProjects.Add(projectuserbacker);
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<List<Project>> GetTrendingProject()
        {
            var max = new float();
            max = 0;
            var return_project_list = new List<Project>();
            var project_list = await _context.Projects.Include(project => project.MyPackages).ToListAsync();
            //Get Max Current Funds
            if (project_list == null)
            {
                return null;
            }
            foreach (var project in project_list)
            {
                if (project.CurrentFunds >= max)
                {
                    max = project.CurrentFunds;
                    return_project_list.Add(project);
                } 
            }
            return_project_list.Reverse();
            if (return_project_list.Count > 2)
            {
                return_project_list.RemoveRange(2, return_project_list.Count - 2);
            }
            return return_project_list;
        }
    }
}
