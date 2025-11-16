using Inventory_System.Data;
using Microsoft.EntityFrameworkCore;

namespace Inventory_System
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ✅ Add MVC services
            builder.Services.AddControllersWithViews();

            // ✅ MySQL connection
            builder.Services.AddDbContext<InventoryDbContext>(options =>
     options.UseMySql(
         builder.Configuration.GetConnectionString("DefaultConnection"),
         ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
     ));


            // ✅ Enable Session
            builder.Services.AddSession();
            builder.Services.AddHttpContextAccessor();

            var app = builder.Build();

            // ✅ Error handling for production
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();
            app.UseSession(); // ✅ Make sure this comes after UseRouting and before MapControllerRoute

            // ✅ Default route — go to Login first
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}");

            app.Run();
        }
    }
}
