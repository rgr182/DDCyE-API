using DDEyC_API.DataAccess.Repositories;
using DDEyC_API.Models;
using OfficeOpenXml;

public interface ICourseImportService
    {
        Task ImportFromExcel(IFormFile file);
    }

public class CourseImportService : ICourseImportService
    {
        private readonly ICourseRepository _repository;
        private readonly ILogger<CourseImportService> _logger;

        public CourseImportService(
            ICourseRepository repository,
            ILogger<CourseImportService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task ImportFromExcel(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets[0];
            var rowCount = worksheet.Dimension.Rows;

            var courses = new List<Course>();
            
            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    if (!DateTime.TryParse(worksheet.Cells[row, 3].Text, out DateTime courseDate))
                    {
                        _logger.LogWarning("Invalid date format in row {Row}: {DateText}", 
                            row, worksheet.Cells[row, 3].Text);
                        continue;
                    }

                    var course = new Course
                    {
                        Title = worksheet.Cells[row, 1].Text.Trim(),
                        Location = worksheet.Cells[row, 2].Text.Trim(),
                        Date = courseDate,
                        Description = worksheet.Cells[row, 4].Text.Trim(),
                        DetailLink = worksheet.Cells[row, 5].Text.Trim(),
                        IsActive = courseDate > DateTime.UtcNow
                    };

                    courses.Add(course);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing row {Row}", row);
                }
            }

            if (courses.Any())
            {
                await _repository.DeleteAllAsync();
                await _repository.BulkInsertAsync(courses);
                _logger.LogInformation("Successfully imported {Count} courses", courses.Count);
            }
        }
    }
