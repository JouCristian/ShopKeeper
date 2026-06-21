using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class CategoryService
    {
        private readonly CategoryRepository _categoryRepository;
        private readonly ProductRepository _productRepository;

        public CategoryService()
        {
            string connectionString = DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath);
            _categoryRepository = new CategoryRepository(connectionString);
            _productRepository = new ProductRepository(connectionString);
        }

        public IList<Category> GetActiveCategories()
        {
            return _categoryRepository.GetActiveCategories();
        }

        public IList<Category> GetAllCategories()
        {
            return _categoryRepository.GetAllCategories();
        }

        public bool TryAdd(string name, out string message)
        {
            name = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                message = "分类名称不能为空。";
                return false;
            }

            if (_categoryRepository.ExistsByName(name, null))
            {
                message = "分类名称已存在。";
                return false;
            }

            _categoryRepository.Add(name);
            message = "分类已新增。";
            return true;
        }

        public bool TryRename(long id, string name, out string message)
        {
            name = NormalizeName(name);
            if (id <= 0)
            {
                message = "请先选择分类。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                message = "分类名称不能为空。";
                return false;
            }

            if (_categoryRepository.ExistsByName(name, id))
            {
                message = "分类名称已存在。";
                return false;
            }

            _categoryRepository.Rename(id, name);
            message = "分类名称已保存。";
            return true;
        }

        public void SetActive(long id, bool isActive)
        {
            _categoryRepository.SetActive(id, isActive);
        }

        public bool HasProducts(long categoryId)
        {
            return _productRepository.HasProductsInCategory(categoryId);
        }

        private static string NormalizeName(string name)
        {
            return (name ?? string.Empty).Trim();
        }
    }
}
