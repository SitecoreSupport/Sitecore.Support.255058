using Sitecore.Buckets.Extensions;
using Sitecore.Buckets.Pipelines.BucketOperations.SyncBucket;
using Sitecore.Collections;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.SecurityModel;
using System;
using System.Linq;

namespace Sitecore.Support.Buckets.Pipelines.BucketOperations.SyncBucket
{
  public class RunSyncProcess : SyncBucketProcessor
  {
    protected class SyncOperationContext
    {
      public int MoveOperationCount
      {
        get;
        protected set;
      }

      public Item Root
      {
        get;
      }

      public SyncOperationContext(Item root)
      {
        Assert.ArgumentNotNull(root, "root");
        Root = root;
      }

      public virtual void IncrementMoveOperations()
      {
        MoveOperationCount++;
      }
    }

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
      SyncOperationContext syncOperationContext = new SyncOperationContext(item);
      foreach (Item child in item.GetChildren(ChildListOptions.SkipSorting))
      {
        SyncRec(child, syncOperationContext);
      }
      Log.Debug($"Bucket Sync has performed {syncOperationContext.MoveOperationCount} moves for item '{AuditFormatter.FormatItem(item)}' ");
      UpdateOperationContext(args, syncOperationContext);
      return true;
    }

    protected virtual void UpdateOperationContext(SyncBucketArgs args, SyncOperationContext context)
    {
      if (args.Context != null)
      {
        args.Context.MoveOperationCount = context.MoveOperationCount;
      }
    }

    private void SyncRec(Item current, SyncOperationContext operationContext)
    {
      foreach (Item child in current.GetChildren(ChildListOptions.SkipSorting))
      {
        SyncRec(child, operationContext);
      }
      if (ShouldBeMovedToRoot(current) && !IsAlreadyOnItsPlace(current, operationContext.Root.Paths.Path))
      {
        MoveItem(current, operationContext.Root);
        operationContext.IncrementMoveOperations();
      }
      if (ShouldMoveToDateFolder(current))
      {
        //if (!IsAlreadyOnItsPlace(current, GetDestinationFolderPath(operationContext.Root, current.Statistics.Created, current)))
        if (!IsAlreadyOnItsPlace(current, GetDestinationFolderPath(operationContext.Root, current.Created, current)))
        {
          MoveSingleItemToDynamicFolder(operationContext.Root, current);
          operationContext.IncrementMoveOperations();
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