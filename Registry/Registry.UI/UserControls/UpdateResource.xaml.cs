﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Practices.ObjectBuilder2;
using Microsoft.Practices.Unity;
using Registry.Common;
using Registry.Communication;
using Registry.Services.Abstract;
using Registry.UI.Extensions;

namespace Registry.UI.UserControls
{
  public partial class UpdateResource : UserControl
  {
    private readonly IResourceService _resourceService = RegistryCommon.Instance.Container.Resolve<IResourceService>();
    private readonly ICategoryService _categoryService = RegistryCommon.Instance.Container.Resolve<ICategoryService>();

    private readonly IResourceGroupService _resourceGroupService =
      RegistryCommon.Instance.Container.Resolve<IResourceGroupService>();

    private GetAllGroupsResult[] _allGroups;
    private GetAllResourcesResult _selectedResource;

    public UpdateResource(GetAllResourcesResult resource)
    {
      InitializeComponent();
      _selectedResource = resource;
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
      RegistryCommon.Instance.MainGrid.OpenUserControlWithSignOut(new Resources());
    }

    private void CategoriesTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {

    }

    private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
      Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
      bool? result = dlg.ShowDialog();
      if (result == true)
      {
        FileNameTextBox.Text = dlg.FileName;
      }
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
      SaveButton.IsEnabled = false;
      RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Saving;

      if (!ValidateFields())
      {
        SaveButton.IsEnabled = true;
        return;
      }

      var selectedCategory = (TreeViewItem) CategoriesTree.SelectedItem;
      var resourceGroups = new List<Guid>();
      foreach (CheckBox item in GroupsListBox.Items)
      {
        if (item.IsChecked == true)
        {
          resourceGroups.Add(Guid.Parse(item.Uid));
        }
      }

      var request = new UpdateResourceRequest
      {
        Id = _selectedResource.Id,
        Name = ResourceTitle.Text,
        Description = ResourceDescription.Text,
        OwnerLogin = RegistryCommon.Instance.Login,
        CategoryId = Guid.Parse(selectedCategory.Uid),
        ResourceGroups = resourceGroups.ToArray(),
        SaveDate = _selectedResource.Id,
        Uid = UniqueIdentifier.Text
      };

      if (string.IsNullOrEmpty(ResourceTags.Text))
      {
        request.Tags = new string[0];
      }
      else
      {
        var tags = ResourceTags.Text.Split(',');
        for (int i = 0; i < tags.Length; i++)
        {
          int count = 0;
          for (int j = 0; j < tags[i].Length; j++)
          {
            if (tags[i][j] != ' ')
            {
              break;
            }

            count++;
          }

          tags[i] = tags[i].Remove(0, count);
        }

        request.Tags = tags;
      }

      try
      {
        if (SetNewFileRadioButton.IsChecked == true)
        {
          using (var fileStream = new FileStream(FileNameTextBox.Text, FileMode.Open))
          {
            request.FileName =
              FileNameTextBox.Text.Substring(FileNameTextBox.Text.LastIndexOf("\\", StringComparison.Ordinal));
            request.Url =
              await
                _resourceService.UploadToBlob(fileStream,
                  $"{request.SaveDate.ToString(CultureInfo.InvariantCulture)}_{request.FileName}");
          }
        }
        else
        {
          request.FileName = _selectedResource.FileName;
          request.Url = _selectedResource.Url;
        }

        await _resourceService.UpdateResource(request);

        RegistryCommon.Instance.MainGrid.OpenUserControlWithSignOut(new Resources());
        RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Saved;
      }
      catch (FaultException ex)
      {
        if (ex.Reason.ToString() == "UidExist")
        {
          MessageBox.Show("Такий iдентифiкатор вже існує", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
          SaveButton.IsEnabled = true;
          RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Failed;
          return;
        }

        throw;
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        SaveButton.IsEnabled = true;
        RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Failed;
      }
    }

    private bool ValidateFields()
    {
      if (SetNewFileRadioButton.IsChecked == true && !File.Exists(FileNameTextBox.Text))
      {
        MessageBox.Show(
          "Шлях до файлу вибрано не вірно",
          "Помилка",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Failed;
        return false;
      }

      if (string.IsNullOrEmpty(UniqueIdentifier.Text))
      {
        MessageBox.Show(
         "Задайте ідентифiкатор ресурса",
         "Помилка",
         MessageBoxButton.OK,
         MessageBoxImage.Error);
        RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Failed;
        return false;
      }

      if (string.IsNullOrEmpty(ResourceTitle.Text))
      {
        MessageBox.Show(
          "Задайте ім'я ресурса",
          "Помилка",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Failed;
        return false;
      }

      if (CategoriesTree.SelectedItem == null)
      {
        MessageBox.Show(
          "Виберіть категорію для ресурса",
          "Помилка",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Failed;
        return false;
      }

      return true;
    }

    private async void UpdateResource_OnLoaded(object sender, RoutedEventArgs e)
    {
      RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Loading;

      GetAllCategoriesResult[] result = await _categoryService.GetAllCategories();
      var baseItem = result.Single(item => item.ParentId == null);
      var resourceDetails = await _resourceService.GetResourceDetails(_selectedResource.Id);
      UniqueIdentifier.Text = resourceDetails.Uid;

      var newTreeItem = new TreeViewItem
      {
        Header = baseItem.Name,
        Uid = baseItem.Id.ToString(),
        IsExpanded = true,
        IsSelected = resourceDetails.Category == baseItem.Id
      };

      CategoriesTree.Items.Add(newTreeItem);
      FillCategories(CategoriesTree.Items[0] as TreeViewItem, baseItem, result, resourceDetails.Category);

      _allGroups = await _resourceGroupService.GetAllResourceGroups();
      for (int i = 0; i < _allGroups.Length; i++)
      {
        GroupsListBox.Items.Add(new CheckBox
        {
          Content = _allGroups[i].Name,
          Uid = _allGroups[i].Id.ToString(),
          IsChecked = resourceDetails.ResourceGroups.Contains(_allGroups[i].Id)
        });
      }

      ResourceDescription.Text = _selectedResource.Description;
      ResourceTitle.Text = _selectedResource.Name;
      ResourceTags.Text = string.Join(", ", resourceDetails.Tags);

      RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Ready;
    }

    private void FillCategories(
      TreeViewItem baseItem,
      GetAllCategoriesResult baseCategory,
      GetAllCategoriesResult[] allCategories,
      Guid selectedId)
    {
      allCategories.Where(item => item.ParentId == baseCategory.Id).ForEach(item =>
      {
        var newItem = new TreeViewItem
        {
          Header = item.Name,
          Uid = item.Id.ToString(),
          IsSelected = selectedId == item.Id
        };

        baseItem.Items.Add(newItem);
        FillCategories(newItem, item, allCategories, selectedId);
      });
    }

    private void SetOldFileRadioButton_OnClick(object sender, RoutedEventArgs e)
    {
      SelectFileGrid.IsEnabled = false;
      FileNameTextBox.Text = string.Empty;
    }

    private void SetNewFileRadioButton_OnClick(object sender, RoutedEventArgs e)
    {
      SelectFileGrid.IsEnabled = true;
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
      MessageBoxResult result = MessageBox.Show(
      "Ви не зможете відмінити цю дію. Ви впевнені, що хочете видалити ресурс?",
      "Підтвердіть операцію",
      MessageBoxButton.YesNoCancel,
      MessageBoxImage.Asterisk);
      if (result != MessageBoxResult.Yes)
      {
        return;
      }

      RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Deleting;

      DeleteButton.IsEnabled = false;
      if (!string.IsNullOrEmpty(_selectedResource.Url))
      {
        await _resourceService.DeleteFromBlob(_selectedResource.Url);
      }

      await _resourceService.DeleteResource(_selectedResource.Id);

      RegistryCommon.Instance.MainProgressBar.Text = StatusBarState.Ready;
      RegistryCommon.Instance.MainGrid.OpenUserControlWithSignOut(new Resources());
    }
  }
}