using BookNest.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Data;

public sealed class BookNestDbContext(DbContextOptions<BookNestDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<BookCategory> BookCategories => Set<BookCategory>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<UserBook> UserBooks => Set<UserBook>();
    public DbSet<ReadingProgress> ReadingProgress => Set<ReadingProgress>();
    public DbSet<ReadingSession> ReadingSessions => Set<ReadingSession>();
    public DbSet<SavedBook> SavedBooks => Set<SavedBook>();
    public DbSet<Download> Downloads => Set<Download>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<Highlight> Highlights => Set<Highlight>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionBook> CollectionBooks => Set<CollectionBook>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<BookViewHistory> BookViewHistory => Set<BookViewHistory>();
    public DbSet<ReadingGoal> ReadingGoals => Set<ReadingGoal>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<BookEntitlement> BookEntitlements => Set<BookEntitlement>();
    public DbSet<CollectionMember> CollectionMembers => Set<CollectionMember>();
    public DbSet<CollectionFavorite> CollectionFavorites => Set<CollectionFavorite>();

    /// <summary>
    /// Maps the database design's tables, columns, keys, relationships and constraints.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            e.ToTable("users", t =>
            {
                t.HasCheckConstraint("ck_users_0", "status IN ('ACTIVE', 'SUSPENDED', 'DELETED')");
            });
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.Email).HasColumnName("email").IsRequired().HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
            e.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired().HasMaxLength(150);
            e.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(1000);
            e.Property(x => x.Bio).HasColumnName("bio").HasMaxLength(100);
            e.Property(x => x.Timezone).HasColumnName("timezone").IsRequired().HasMaxLength(64).HasDefaultValueSql("'Asia/Tokyo'");
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValueSql("'ACTIVE'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<Book>(e =>
        {
            e.ToTable("books", t =>
            {
                t.HasCheckConstraint("ck_books_0", "page_count > 0 AND purchase_price >= 0 AND rental_price >= 0 AND rental_days > 0 AND content_version > 0");
                t.HasCheckConstraint("ck_books_1", "file_size_bytes IS NULL OR file_size_bytes >= 0");
                t.HasCheckConstraint("ck_books_2", "status IN ('DRAFT', 'PUBLISHED', 'ARCHIVED')");
                t.HasCheckConstraint("ck_books_3", "file_format IN ('PDF', 'EPUB')");
            });
            e.HasKey(x => x.BookId);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.Title).HasColumnName("title").IsRequired().HasMaxLength(255);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.CoverImageUrl).HasColumnName("cover_image_url").HasMaxLength(1000);
            e.Property(x => x.Isbn).HasColumnName("isbn").HasMaxLength(20);
            e.HasIndex(x => x.Isbn).IsUnique();
            e.Property(x => x.PublisherName).HasColumnName("publisher_name").HasMaxLength(255);
            e.Property(x => x.PublishedDate).HasColumnName("published_date");
            e.Property(x => x.LanguageCode).HasColumnName("language_code").IsRequired().HasMaxLength(35);
            e.Property(x => x.PageCount).HasColumnName("page_count").IsRequired();
            e.Property(x => x.PurchasePrice).HasColumnName("purchase_price").IsRequired().HasPrecision(12, 2).HasDefaultValueSql("0");
            e.Property(x => x.RentalPrice).HasColumnName("rental_price").IsRequired().HasPrecision(12, 2).HasDefaultValueSql("0");
            e.Property(x => x.CurrencyCode).HasColumnName("currency_code").IsRequired().HasMaxLength(3).HasDefaultValueSql("'JPY'");
            e.Property(x => x.RentalDays).HasColumnName("rental_days").IsRequired().HasDefaultValueSql("30");
            e.Property(x => x.PreviewUrl).HasColumnName("preview_url").HasMaxLength(1000);
            e.Property(x => x.ContentStorageKey).HasColumnName("content_storage_key").IsRequired().HasMaxLength(1000);
            e.Property(x => x.FileFormat).HasColumnName("file_format").IsRequired().HasMaxLength(10).HasDefaultValueSql("'PDF'");
            e.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
            e.Property(x => x.ContentVersion).HasColumnName("content_version").IsRequired().HasDefaultValueSql("1");
            e.Property(x => x.PremiumEligible).HasColumnName("premium_eligible").IsRequired().HasDefaultValue(true);
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValueSql("'DRAFT'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<Author>(e =>
        {
            e.ToTable("authors");
            e.HasKey(x => x.AuthorId);
            e.Property(x => x.AuthorId).HasColumnName("author_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.AuthorName).HasColumnName("author_name").IsRequired().HasMaxLength(150);
            e.Property(x => x.Bio).HasColumnName("bio");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<BookAuthor>(e =>
        {
            e.ToTable("book_authors", t =>
            {
                t.HasCheckConstraint("ck_book_authors_0", "display_order > 0");
            });
            e.HasKey(x => new
            {
                x.BookId,
                x.AuthorId
            });
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.AuthorId).HasColumnName("author_id").IsRequired();
            e.HasOne<Author>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired().HasDefaultValueSql("1");
        });
        model.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.CategoryId).HasColumnName("category_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.ParentCategoryId).HasColumnName("parent_category_id");
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            e.Property(x => x.Slug).HasColumnName("slug").IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.IconKey).HasColumnName("icon_key").HasMaxLength(100);
            e.Property(x => x.BannerImageUrl).HasColumnName("banner_image_url").HasMaxLength(1000);
            e.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired().HasDefaultValueSql("0");
            e.Property(x => x.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<BookCategory>(e =>
        {
            e.ToTable("book_categories");
            e.HasKey(x => new
            {
                x.BookId,
                x.CategoryId
            });
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.CategoryId).HasColumnName("category_id").IsRequired();
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.IsPrimary).HasColumnName("is_primary").IsRequired().HasDefaultValue(false);
            e.HasIndex(x => x.BookId).IsUnique().HasFilter("is_primary = TRUE");
        });
        model.Entity<Chapter>(e =>
        {
            e.ToTable("chapters", t =>
            {
                t.HasCheckConstraint("ck_chapters_0", "chapter_no > 0 AND start_page >= 1 AND end_page >= start_page");
            });
            e.HasKey(x => x.ChapterId);
            e.Property(x => x.ChapterId).HasColumnName("chapter_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ChapterNo).HasColumnName("chapter_no").IsRequired();
            e.Property(x => x.Title).HasColumnName("title").IsRequired().HasMaxLength(255);
            e.Property(x => x.StartPage).HasColumnName("start_page").IsRequired();
            e.Property(x => x.EndPage).HasColumnName("end_page").IsRequired();
            e.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new
            {
                x.BookId,
                x.ChapterNo
            }).IsUnique();
            e.HasIndex(x => new
            {
                x.BookId,
                x.ChapterId
            }).IsUnique();
        });
        model.Entity<UserBook>(e =>
        {
            e.ToTable("user_books");
            e.HasKey(x => x.UserBookId);
            e.Property(x => x.UserBookId).HasColumnName("user_book_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.AddedAt).HasColumnName("added_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.ArchivedAt).HasColumnName("archived_at");
            e.HasIndex(x => new
            {
                x.UserId,
                x.BookId
            }).IsUnique();
        });
        model.Entity<ReadingProgress>(e =>
        {
            e.ToTable("reading_progress", t =>
            {
                t.HasCheckConstraint("ck_reading_progress_0", "current_page >= 0");
                t.HasCheckConstraint("ck_reading_progress_1", "status <> 'FINISHED' OR finished_at IS NOT NULL");
                t.HasCheckConstraint("ck_reading_progress_2", "status IN ('NOT_STARTED', 'READING', 'FINISHED')");
            });
            e.HasKey(x => x.ProgressId);
            e.Property(x => x.ProgressId).HasColumnName("progress_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ChapterId).HasColumnName("chapter_id");
            e.Property(x => x.CurrentPage).HasColumnName("current_page").IsRequired().HasDefaultValueSql("0");
            e.Property(x => x.LocationToken).HasColumnName("location_token").HasMaxLength(500);
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValueSql("'NOT_STARTED'");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.LastReadAt).HasColumnName("last_read_at");
            e.Property(x => x.FinishedAt).HasColumnName("finished_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new
            {
                x.UserId,
                x.BookId
            }).IsUnique();
            e.HasOne<UserBook>().WithMany().HasForeignKey(x => new
            {
                x.UserId,
                x.BookId
            }).HasPrincipalKey(x => new
            {
                x.UserId,
                x.BookId
            }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Chapter>().WithMany().HasForeignKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).HasPrincipalKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ReadingSession>(e =>
        {
            e.ToTable("reading_sessions", t =>
            {
                t.HasCheckConstraint("ck_reading_sessions_0", "active_seconds >= 0 AND pages_read >= 0 AND start_page >= 0 AND end_page >= 0");
                t.HasCheckConstraint("ck_reading_sessions_1", "ended_at IS NULL OR (ended_at >= started_at AND active_seconds <= EXTRACT(EPOCH FROM ended_at - started_at))");
            });
            e.HasKey(x => x.SessionId);
            e.Property(x => x.SessionId).HasColumnName("session_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ChapterId).HasColumnName("chapter_id");
            e.Property(x => x.ClientSessionKey).HasColumnName("client_session_key").IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.ClientSessionKey).IsUnique();
            e.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
            e.Property(x => x.EndedAt).HasColumnName("ended_at");
            e.Property(x => x.ActiveSeconds).HasColumnName("active_seconds").IsRequired().HasDefaultValueSql("0");
            e.Property(x => x.StartPage).HasColumnName("start_page").IsRequired().HasDefaultValueSql("0");
            e.Property(x => x.EndPage).HasColumnName("end_page").IsRequired().HasDefaultValueSql("0");
            e.Property(x => x.PagesRead).HasColumnName("pages_read").IsRequired().HasDefaultValueSql("0");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne<UserBook>().WithMany().HasForeignKey(x => new
            {
                x.UserId,
                x.BookId
            }).HasPrincipalKey(x => new
            {
                x.UserId,
                x.BookId
            }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Chapter>().WithMany().HasForeignKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).HasPrincipalKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new
            {
                x.UserId,
                x.StartedAt
            });
        });
        model.Entity<SavedBook>(e =>
        {
            e.ToTable("saved_books");
            e.HasKey(x => new
            {
                x.UserId,
                x.BookId
            });
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SavedAt).HasColumnName("saved_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<Download>(e =>
        {
            e.ToTable("downloads", t =>
            {
                t.HasCheckConstraint("ck_downloads_0", "bytes_downloaded >= 0 AND content_version > 0");
                t.HasCheckConstraint("ck_downloads_1", "status <> 'COMPLETED' OR downloaded_at IS NOT NULL");
                t.HasCheckConstraint("ck_downloads_2", "status IN ('QUEUED', 'DOWNLOADING', 'COMPLETED', 'FAILED', 'REMOVED')");
            });
            e.HasKey(x => x.DownloadId);
            e.Property(x => x.DownloadId).HasColumnName("download_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.DeviceKey).HasColumnName("device_key").IsRequired().HasMaxLength(100);
            e.Property(x => x.ContentVersion).HasColumnName("content_version").IsRequired().HasDefaultValueSql("1");
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValueSql("'QUEUED'");
            e.Property(x => x.BytesDownloaded).HasColumnName("bytes_downloaded").IsRequired().HasDefaultValueSql("0");
            e.Property(x => x.DownloadedAt).HasColumnName("downloaded_at");
            e.Property(x => x.AccessExpiresAt).HasColumnName("access_expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new
            {
                x.UserId,
                x.BookId,
                x.DeviceKey
            }).IsUnique();
        });
        model.Entity<Note>(e =>
        {
            e.ToTable("notes", t =>
            {
                t.HasCheckConstraint("ck_notes_0", "page_no IS NOT NULL OR NULLIF(location_token, '') IS NOT NULL");
                t.HasCheckConstraint("ck_notes_1", "page_no IS NULL OR page_no > 0");
            });
            e.HasKey(x => x.NoteId);
            e.Property(x => x.NoteId).HasColumnName("note_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ChapterId).HasColumnName("chapter_id");
            e.Property(x => x.PageNo).HasColumnName("page_no");
            e.Property(x => x.LocationToken).HasColumnName("location_token").HasMaxLength(500);
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(150);
            e.Property(x => x.NoteText).HasColumnName("note_text").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne<Chapter>().WithMany().HasForeignKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).HasPrincipalKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Bookmark>(e =>
        {
            e.ToTable("bookmarks", t =>
            {
                t.HasCheckConstraint("ck_bookmarks_0", "page_no IS NOT NULL OR NULLIF(location_token, '') IS NOT NULL");
                t.HasCheckConstraint("ck_bookmarks_1", "page_no IS NULL OR page_no > 0");
            });
            e.HasKey(x => x.BookmarkId);
            e.Property(x => x.BookmarkId).HasColumnName("bookmark_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ChapterId).HasColumnName("chapter_id");
            e.Property(x => x.PageNo).HasColumnName("page_no");
            e.Property(x => x.LocationToken).HasColumnName("location_token").HasMaxLength(500);
            e.Property(x => x.Label).HasColumnName("label").HasMaxLength(150);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne<Chapter>().WithMany().HasForeignKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).HasPrincipalKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new
            {
                x.UserId,
                x.BookId,
                x.LocationToken
            }).IsUnique().HasFilter("location_token IS NOT NULL");
        });
        model.Entity<Highlight>(e =>
        {
            e.ToTable("highlights", t =>
            {
                t.HasCheckConstraint("ck_highlights_0", "page_no IS NOT NULL OR NULLIF(location_token, '') IS NOT NULL");
                t.HasCheckConstraint("ck_highlights_1", "page_no IS NULL OR page_no > 0");
                t.HasCheckConstraint("ck_highlights_2", "color_code IN ('YELLOW', 'GREEN', 'BLUE', 'PINK')");
            });
            e.HasKey(x => x.HighlightId);
            e.Property(x => x.HighlightId).HasColumnName("highlight_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ChapterId).HasColumnName("chapter_id");
            e.Property(x => x.PageNo).HasColumnName("page_no");
            e.Property(x => x.LocationToken).HasColumnName("location_token").HasMaxLength(500);
            e.Property(x => x.HighlightedText).HasColumnName("highlighted_text").IsRequired();
            e.Property(x => x.StartAnchor).HasColumnName("start_anchor").IsRequired().HasMaxLength(500);
            e.Property(x => x.EndAnchor).HasColumnName("end_anchor").IsRequired().HasMaxLength(500);
            e.Property(x => x.ColorCode).HasColumnName("color_code").IsRequired().HasMaxLength(10).HasDefaultValueSql("'YELLOW'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne<Chapter>().WithMany().HasForeignKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).HasPrincipalKey(x => new
            {
                x.BookId,
                x.ChapterId
            }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Review>(e =>
        {
            e.ToTable("reviews", t =>
            {
                t.HasCheckConstraint("ck_reviews_0", "rating BETWEEN 1 AND 5");
                t.HasCheckConstraint("ck_reviews_1", "status IN ('PUBLISHED', 'HIDDEN')");
            });
            e.HasKey(x => x.ReviewId);
            e.Property(x => x.ReviewId).HasColumnName("review_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Rating).HasColumnName("rating").IsRequired();
            e.Property(x => x.ReviewText).HasColumnName("review_text");
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValueSql("'PUBLISHED'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new
            {
                x.UserId,
                x.BookId
            }).IsUnique();
        });
        model.Entity<Collection>(e =>
        {
            e.ToTable("collections", t =>
            {
                t.HasCheckConstraint("ck_collections_0", "visibility IN ('PRIVATE', 'SHARED', 'PUBLIC')");
            });
            e.HasKey(x => x.CollectionId);
            e.Property(x => x.CollectionId).HasColumnName("collection_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            e.Property(x => x.CoverImageUrl).HasColumnName("cover_image_url").HasMaxLength(1000);
            e.Property(x => x.Visibility).HasColumnName("visibility").IsRequired().HasMaxLength(20).HasDefaultValueSql("'PRIVATE'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new
            {
                x.UserId,
                x.Name
            }).IsUnique();
        });
        model.Entity<CollectionBook>(e =>
        {
            e.ToTable("collection_books");
            e.HasKey(x => new
            {
                x.CollectionId,
                x.BookId
            });
            e.Property(x => x.CollectionId).HasColumnName("collection_id").IsRequired();
            e.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired().HasDefaultValueSql("0");
            e.Property(x => x.AddedAt).HasColumnName("added_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<Order>(e =>
        {
            e.ToTable("orders", t =>
            {
                t.HasCheckConstraint("ck_orders_0", "total_amount >= 0");
                t.HasCheckConstraint("ck_orders_1", "status NOT IN ('PAID','REFUNDED') OR paid_at IS NOT NULL");
                t.HasCheckConstraint("ck_orders_2", "status IN ('PENDING', 'PAID', 'FAILED', 'CANCELLED', 'REFUNDED')");
            });
            e.HasKey(x => x.OrderId);
            e.Property(x => x.OrderId).HasColumnName("order_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.OrderNumber).HasColumnName("order_number").IsRequired().HasMaxLength(40);
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValueSql("'PENDING'");
            e.Property(x => x.CurrencyCode).HasColumnName("currency_code").IsRequired().HasMaxLength(3).HasDefaultValueSql("'JPY'");
            e.Property(x => x.TotalAmount).HasColumnName("total_amount").IsRequired().HasPrecision(12, 2).HasDefaultValueSql("0");
            e.Property(x => x.PaymentReference).HasColumnName("payment_reference").HasMaxLength(150);
            e.HasIndex(x => x.PaymentReference).IsUnique();
            e.Property(x => x.PaidAt).HasColumnName("paid_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items", t =>
            {
                t.HasCheckConstraint("ck_order_items_0", "quantity = 1 AND unit_price >= 0 AND line_total = unit_price");
                t.HasCheckConstraint("ck_order_items_1", "(access_type = 'RENTAL' AND rental_days > 0) OR (access_type = 'PURCHASE' AND rental_days IS NULL)");
                t.HasCheckConstraint("ck_order_items_2", "access_type IN ('PURCHASE', 'RENTAL')");
            });
            e.HasKey(x => x.OrderItemId);
            e.Property(x => x.OrderItemId).HasColumnName("order_item_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.AccessType).HasColumnName("access_type").IsRequired().HasMaxLength(20);
            e.Property(x => x.UnitPrice).HasColumnName("unit_price").IsRequired().HasPrecision(12, 2).HasDefaultValueSql("0");
            e.Property(x => x.Quantity).HasColumnName("quantity").IsRequired().HasDefaultValueSql("1");
            e.Property(x => x.LineTotal).HasColumnName("line_total").IsRequired().HasPrecision(12, 2).HasDefaultValueSql("0");
            e.Property(x => x.RentalDays).HasColumnName("rental_days");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new
            {
                x.OrderId,
                x.BookId,
                x.AccessType
            }).IsUnique();
        });
        model.Entity<BookViewHistory>(e =>
        {
            e.ToTable("book_view_history");
            e.HasKey(x => x.ViewId);
            e.Property(x => x.ViewId).HasColumnName("view_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ViewedAt).HasColumnName("viewed_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.EventKey).HasColumnName("event_key").IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.EventKey).IsUnique();
            e.HasIndex(x => new
            {
                x.UserId,
                x.ViewedAt
            });
        });
        model.Entity<ReadingGoal>(e =>
        {
            e.ToTable("reading_goals", t =>
            {
                t.HasCheckConstraint("ck_reading_goals_0", "target_books > 0 AND target_year BETWEEN 1 AND 9999");
            });
            e.HasKey(x => x.GoalId);
            e.Property(x => x.GoalId).HasColumnName("goal_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.TargetYear).HasColumnName("target_year").IsRequired();
            e.Property(x => x.TargetBooks).HasColumnName("target_books").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new
            {
                x.UserId,
                x.TargetYear
            }).IsUnique();
        });
        model.Entity<SubscriptionPlan>(e =>
        {
            e.ToTable("subscription_plans", t =>
            {
                t.HasCheckConstraint("ck_subscription_plans_0", "price >= 0");
                t.HasCheckConstraint("ck_subscription_plans_1", "billing_interval IN ('MONTH', 'YEAR')");
            });
            e.HasKey(x => x.PlanId);
            e.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.PlanCode).HasColumnName("plan_code").IsRequired().HasMaxLength(30);
            e.HasIndex(x => x.PlanCode).IsUnique();
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
            e.Property(x => x.Price).HasColumnName("price").IsRequired().HasPrecision(12, 2).HasDefaultValueSql("0");
            e.Property(x => x.CurrencyCode).HasColumnName("currency_code").IsRequired().HasMaxLength(3).HasDefaultValueSql("'JPY'");
            e.Property(x => x.BillingInterval).HasColumnName("billing_interval").IsRequired().HasMaxLength(20);
            e.Property(x => x.OfflineAccess).HasColumnName("offline_access").IsRequired().HasDefaultValue(true);
            e.Property(x => x.AdFree).HasColumnName("ad_free").IsRequired().HasDefaultValue(true);
            e.Property(x => x.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<UserSubscription>(e =>
        {
            e.ToTable("user_subscriptions", t =>
            {
                t.HasCheckConstraint("ck_user_subscriptions_0", "ends_at > started_at");
                t.HasCheckConstraint("ck_user_subscriptions_1", "status IN ('PENDING', 'ACTIVE', 'CANCELLED', 'EXPIRED')");
            });
            e.HasKey(x => x.SubscriptionId);
            e.Property(x => x.SubscriptionId).HasColumnName("subscription_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
            e.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValueSql("'PENDING'");
            e.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
            e.Property(x => x.EndsAt).HasColumnName("ends_at").IsRequired();
            e.Property(x => x.CancelAtPeriodEnd).HasColumnName("cancel_at_period_end").IsRequired().HasDefaultValue(false);
            e.Property(x => x.ProviderReference).HasColumnName("provider_reference").HasMaxLength(150);
            e.HasIndex(x => x.ProviderReference).IsUnique();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("status = 'ACTIVE'");
        });
        model.Entity<BookEntitlement>(e =>
        {
            e.ToTable("book_entitlements", t =>
            {
                t.HasCheckConstraint("ck_book_entitlements_0", "(source_type IN ('PURCHASE','RENTAL') AND order_item_id IS NOT NULL AND subscription_id IS NULL) OR (source_type = 'PREMIUM' AND subscription_id IS NOT NULL AND order_item_id IS NULL)");
                t.HasCheckConstraint("ck_book_entitlements_1", "expires_at IS NULL OR expires_at > starts_at");
                t.HasCheckConstraint("ck_book_entitlements_2", "source_type IN ('PURCHASE', 'RENTAL', 'PREMIUM')");
            });
            e.HasKey(x => x.EntitlementId);
            e.Property(x => x.EntitlementId).HasColumnName("entitlement_id").IsRequired().UseIdentityAlwaysColumn();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.BookId).HasColumnName("book_id").IsRequired();
            e.HasOne<Book>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.OrderItemId).HasColumnName("order_item_id");
            e.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
            e.HasOne<UserSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SourceType).HasColumnName("source_type").IsRequired().HasMaxLength(20);
            e.Property(x => x.StartsAt).HasColumnName("starts_at").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.OrderItemId).IsUnique();
        });
        model.Entity<CollectionMember>(e =>
        {
            e.ToTable("collection_members", t =>
            {
                t.HasCheckConstraint("ck_collection_members_0", "role IN ('VIEWER', 'EDITOR')");
                t.HasCheckConstraint("ck_collection_members_1", "status IN ('INVITED', 'ACCEPTED', 'DECLINED', 'REVOKED')");
            });
            e.HasKey(x => new
            {
                x.CollectionId,
                x.UserId
            });
            e.Property(x => x.CollectionId).HasColumnName("collection_id").IsRequired();
            e.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Role).HasColumnName("role").IsRequired().HasMaxLength(20).HasDefaultValueSql("'VIEWER'");
            e.Property(x => x.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValueSql("'INVITED'");
            e.Property(x => x.JoinedAt).HasColumnName("joined_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        model.Entity<CollectionFavorite>(e =>
        {
            e.ToTable("collection_favorites");
            e.HasKey(x => new
            {
                x.UserId,
                x.CollectionId
            });
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.CollectionId).HasColumnName("collection_id").IsRequired();
            e.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.FavoritedAt).HasColumnName("favorited_at").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    /// <summary>
    /// Updates modification timestamps on changed entities before saving them.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Modified))
            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTimeOffset.UtcNow;
            }

        return base.SaveChangesAsync(cancellationToken);
    }
}
