using Sitecore.Buckets.Extensions;
using Sitecore.Buckets.Pipelines.BucketOperations.SyncBucket;
using Sitecore.Collections;
using Sitecore.Data.Items;
using Sitecore.SecurityModel;
using System;
using System.Linq;

namespace Sitecore.Support.Buckets.Pipelines.BucketOperations.SyncBucket
{
  public class RunSyncProcess : SyncBucketProcessor
  {
    public override void Process(SyncBucketArgs args)
    {
      if (args != null && !args.BucketSynced)
      {
        Item item = args.Item;
        if (item != null)
        {
          using (new SecurityDisabler())
          {
            args.BucketSynced = SyncBucket(item, args);
          }
        }
      }
    }

    protected virtual bool SyncBucket(Item item, SyncBucketArgs args)
    {
      if (!item.IsABucket())
      {
        return false;
      }
      foreach (Item child in item.GetChildren(ChildListOptions.SkipSorting))
      {
        SyncRec(item, child);
      }
      return true;
    }

    private void SyncRec(Item root, Item current)
    {
      foreach (Item child in current.GetChildren(ChildListOptions.SkipSorting))
      {
        SyncRec(root, child);
      }
      if (ShouldBeMovedToRoot(current) && !IsAlreadyOnItsPlace(current, root.Paths.Path))
      {
        MoveItem(current, root);
      }
      if (ShouldMoveToDateFolder(current))
      {
        //if (!IsAlreadyOnItsPlace(current, GetDestinationFolderPath(root, current.Statistics.Created, current)))
        if (!IsAlreadyOnItsPlace(current, GetDestinationFolderPath(root, current.Created, current)))
        {
          MoveSingleItemToDynamicFolder(root, current);
        }
      }
      else if (ShouldDeleteInCreationOfBucket(current) && !current.GetChildren(ChildListOptions.SkipSorting).Any())
      {
        current.Delete();
      }
    }

    protected virtual bool IsAlreadyOnItsPlace(Item itemToCheck, string itemPlacePath)
    {
      return itemToCheck.Paths.ParentPath.Equals(itemPlacePath, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldBeMovedToRoot(Item item)
    {
      if (!ShouldDeleteInCreationOfBucket(item) && !item.IsItemBucketable())
      {
        //return !item.Parent.IsLockedChildRelationship();
        return !IsLockedChildRelationship(item.Parent);
      }
      return false;
    }

    internal static bool IsLockedChildRelationship(Item item)
    {
      if (item != null && item.Fields[Sitecore.Buckets.Util.Constants.ShouldNotOrganizeInBucket] != null)
      {
        return item.Fields[Sitecore.Buckets.Util.Constants.ShouldNotOrganizeInBucket].Value == "1";
      }
      return true;
    }
  }
}