CREATE TABLE authors (
    author_id bigint GENERATED ALWAYS AS IDENTITY,
    author_name character varying(150) NOT NULL,
    bio text,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_authors" PRIMARY KEY (author_id)
);


CREATE TABLE books (
    book_id bigint GENERATED ALWAYS AS IDENTITY,
    title character varying(255) NOT NULL,
    description text,
    cover_image_url character varying(1000),
    isbn character varying(20),
    publisher_name character varying(255),
    published_date date,
    language_code character varying(35) NOT NULL,
    page_count integer NOT NULL,
    purchase_price numeric(12,2) NOT NULL DEFAULT (0),
    rental_price numeric(12,2) NOT NULL DEFAULT (0),
    currency_code character varying(3) NOT NULL DEFAULT ('JPY'),
    rental_days integer NOT NULL DEFAULT (30),
    preview_url character varying(1000),
    content_storage_key character varying(1000) NOT NULL,
    file_format character varying(10) NOT NULL DEFAULT ('PDF'),
    file_size_bytes bigint,
    content_version integer NOT NULL DEFAULT (1),
    premium_eligible boolean NOT NULL DEFAULT TRUE,
    status character varying(20) NOT NULL DEFAULT ('DRAFT'),
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_books" PRIMARY KEY (book_id),
    CONSTRAINT ck_books_0 CHECK (page_count > 0 AND purchase_price >= 0 AND rental_price >= 0 AND rental_days > 0 AND content_version > 0),
    CONSTRAINT ck_books_1 CHECK (file_size_bytes IS NULL OR file_size_bytes >= 0),
    CONSTRAINT ck_books_2 CHECK (status IN ('DRAFT', 'PUBLISHED', 'ARCHIVED')),
    CONSTRAINT ck_books_3 CHECK (file_format IN ('PDF', 'EPUB'))
);


CREATE TABLE categories (
    category_id bigint GENERATED ALWAYS AS IDENTITY,
    parent_category_id bigint,
    name character varying(100) NOT NULL,
    slug character varying(100) NOT NULL,
    description text,
    icon_key character varying(100),
    banner_image_url character varying(1000),
    sort_order integer NOT NULL DEFAULT (0),
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_categories" PRIMARY KEY (category_id),
    CONSTRAINT "FK_categories_categories_parent_category_id" FOREIGN KEY (parent_category_id) REFERENCES categories (category_id) ON DELETE RESTRICT
);


CREATE TABLE subscription_plans (
    plan_id bigint GENERATED ALWAYS AS IDENTITY,
    plan_code character varying(30) NOT NULL,
    name character varying(100) NOT NULL,
    price numeric(12,2) NOT NULL DEFAULT (0),
    currency_code character varying(3) NOT NULL DEFAULT ('JPY'),
    billing_interval character varying(20) NOT NULL,
    offline_access boolean NOT NULL DEFAULT TRUE,
    ad_free boolean NOT NULL DEFAULT TRUE,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_subscription_plans" PRIMARY KEY (plan_id),
    CONSTRAINT ck_subscription_plans_0 CHECK (price >= 0),
    CONSTRAINT ck_subscription_plans_1 CHECK (billing_interval IN ('MONTH', 'YEAR'))
);


CREATE TABLE users (
    user_id bigint GENERATED ALWAYS AS IDENTITY,
    email character varying(254) NOT NULL,
    password_hash character varying(255),
    display_name character varying(150) NOT NULL,
    avatar_url character varying(1000),
    bio character varying(100),
    timezone character varying(64) NOT NULL DEFAULT ('Asia/Tokyo'),
    status character varying(20) NOT NULL DEFAULT ('ACTIVE'),
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_users" PRIMARY KEY (user_id),
    CONSTRAINT ck_users_0 CHECK (status IN ('ACTIVE', 'SUSPENDED', 'DELETED'))
);


CREATE TABLE book_authors (
    book_id bigint NOT NULL,
    author_id bigint NOT NULL,
    display_order integer NOT NULL DEFAULT (1),
    CONSTRAINT "PK_book_authors" PRIMARY KEY (book_id, author_id),
    CONSTRAINT ck_book_authors_0 CHECK (display_order > 0),
    CONSTRAINT "FK_book_authors_authors_author_id" FOREIGN KEY (author_id) REFERENCES authors (author_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_book_authors_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT
);


CREATE TABLE chapters (
    chapter_id bigint GENERATED ALWAYS AS IDENTITY,
    book_id bigint NOT NULL,
    chapter_no integer NOT NULL,
    title character varying(255) NOT NULL,
    start_page integer NOT NULL,
    end_page integer NOT NULL,
    sort_order integer NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_chapters" PRIMARY KEY (chapter_id),
    CONSTRAINT "AK_chapters_book_id_chapter_id" UNIQUE (book_id, chapter_id),
    CONSTRAINT ck_chapters_0 CHECK (chapter_no > 0 AND start_page >= 1 AND end_page >= start_page),
    CONSTRAINT "FK_chapters_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT
);


CREATE TABLE book_categories (
    book_id bigint NOT NULL,
    category_id bigint NOT NULL,
    is_primary boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_book_categories" PRIMARY KEY (book_id, category_id),
    CONSTRAINT "FK_book_categories_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_book_categories_categories_category_id" FOREIGN KEY (category_id) REFERENCES categories (category_id) ON DELETE RESTRICT
);


CREATE TABLE book_view_history (
    view_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    viewed_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    event_key character varying(100) NOT NULL,
    CONSTRAINT "PK_book_view_history" PRIMARY KEY (view_id),
    CONSTRAINT "FK_book_view_history_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_book_view_history_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE collections (
    collection_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500),
    cover_image_url character varying(1000),
    visibility character varying(20) NOT NULL DEFAULT ('PRIVATE'),
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_collections" PRIMARY KEY (collection_id),
    CONSTRAINT ck_collections_0 CHECK (visibility IN ('PRIVATE', 'SHARED', 'PUBLIC')),
    CONSTRAINT "FK_collections_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE downloads (
    download_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    device_key character varying(100) NOT NULL,
    content_version integer NOT NULL DEFAULT (1),
    status character varying(20) NOT NULL DEFAULT ('QUEUED'),
    bytes_downloaded bigint NOT NULL DEFAULT (0),
    downloaded_at timestamp with time zone,
    access_expires_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_downloads" PRIMARY KEY (download_id),
    CONSTRAINT ck_downloads_0 CHECK (bytes_downloaded >= 0 AND content_version > 0),
    CONSTRAINT ck_downloads_1 CHECK (status <> 'COMPLETED' OR downloaded_at IS NOT NULL),
    CONSTRAINT ck_downloads_2 CHECK (status IN ('QUEUED', 'DOWNLOADING', 'COMPLETED', 'FAILED', 'REMOVED')),
    CONSTRAINT "FK_downloads_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_downloads_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE orders (
    order_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    order_number character varying(40) NOT NULL,
    status character varying(20) NOT NULL DEFAULT ('PENDING'),
    currency_code character varying(3) NOT NULL DEFAULT ('JPY'),
    total_amount numeric(12,2) NOT NULL DEFAULT (0),
    payment_reference character varying(150),
    paid_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_orders" PRIMARY KEY (order_id),
    CONSTRAINT ck_orders_0 CHECK (total_amount >= 0),
    CONSTRAINT ck_orders_1 CHECK (status NOT IN ('PAID','REFUNDED') OR paid_at IS NOT NULL),
    CONSTRAINT ck_orders_2 CHECK (status IN ('PENDING', 'PAID', 'FAILED', 'CANCELLED', 'REFUNDED')),
    CONSTRAINT "FK_orders_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE reading_goals (
    goal_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    target_year integer NOT NULL,
    target_books integer NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_reading_goals" PRIMARY KEY (goal_id),
    CONSTRAINT ck_reading_goals_0 CHECK (target_books > 0 AND target_year BETWEEN 1 AND 9999),
    CONSTRAINT "FK_reading_goals_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE reviews (
    review_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    rating integer NOT NULL,
    review_text text,
    status character varying(20) NOT NULL DEFAULT ('PUBLISHED'),
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_reviews" PRIMARY KEY (review_id),
    CONSTRAINT ck_reviews_0 CHECK (rating BETWEEN 1 AND 5),
    CONSTRAINT ck_reviews_1 CHECK (status IN ('PUBLISHED', 'HIDDEN')),
    CONSTRAINT "FK_reviews_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reviews_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE saved_books (
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    saved_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_saved_books" PRIMARY KEY (user_id, book_id),
    CONSTRAINT "FK_saved_books_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_saved_books_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE user_books (
    user_book_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    added_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    archived_at timestamp with time zone,
    CONSTRAINT "PK_user_books" PRIMARY KEY (user_book_id),
    CONSTRAINT "AK_user_books_user_id_book_id" UNIQUE (user_id, book_id),
    CONSTRAINT "FK_user_books_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_user_books_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE user_subscriptions (
    subscription_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    plan_id bigint NOT NULL,
    status character varying(20) NOT NULL DEFAULT ('PENDING'),
    started_at timestamp with time zone NOT NULL,
    ends_at timestamp with time zone NOT NULL,
    cancel_at_period_end boolean NOT NULL DEFAULT FALSE,
    provider_reference character varying(150),
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_user_subscriptions" PRIMARY KEY (subscription_id),
    CONSTRAINT ck_user_subscriptions_0 CHECK (ends_at > started_at),
    CONSTRAINT ck_user_subscriptions_1 CHECK (status IN ('PENDING', 'ACTIVE', 'CANCELLED', 'EXPIRED')),
    CONSTRAINT "FK_user_subscriptions_subscription_plans_plan_id" FOREIGN KEY (plan_id) REFERENCES subscription_plans (plan_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_user_subscriptions_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE bookmarks (
    bookmark_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    chapter_id bigint,
    page_no integer,
    location_token character varying(500),
    label character varying(150),
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_bookmarks" PRIMARY KEY (bookmark_id),
    CONSTRAINT ck_bookmarks_0 CHECK (page_no IS NOT NULL OR NULLIF(location_token, '') IS NOT NULL),
    CONSTRAINT ck_bookmarks_1 CHECK (page_no IS NULL OR page_no > 0),
    CONSTRAINT "FK_bookmarks_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_bookmarks_chapters_book_id_chapter_id" FOREIGN KEY (book_id, chapter_id) REFERENCES chapters (book_id, chapter_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_bookmarks_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE highlights (
    highlight_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    chapter_id bigint,
    page_no integer,
    location_token character varying(500),
    highlighted_text text NOT NULL,
    start_anchor character varying(500) NOT NULL,
    end_anchor character varying(500) NOT NULL,
    color_code character varying(10) NOT NULL DEFAULT ('YELLOW'),
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_highlights" PRIMARY KEY (highlight_id),
    CONSTRAINT ck_highlights_0 CHECK (page_no IS NOT NULL OR NULLIF(location_token, '') IS NOT NULL),
    CONSTRAINT ck_highlights_1 CHECK (page_no IS NULL OR page_no > 0),
    CONSTRAINT ck_highlights_2 CHECK (color_code IN ('YELLOW', 'GREEN', 'BLUE', 'PINK')),
    CONSTRAINT "FK_highlights_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_highlights_chapters_book_id_chapter_id" FOREIGN KEY (book_id, chapter_id) REFERENCES chapters (book_id, chapter_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_highlights_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE notes (
    note_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    chapter_id bigint,
    page_no integer,
    location_token character varying(500),
    title character varying(150),
    note_text text NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_notes" PRIMARY KEY (note_id),
    CONSTRAINT ck_notes_0 CHECK (page_no IS NOT NULL OR NULLIF(location_token, '') IS NOT NULL),
    CONSTRAINT ck_notes_1 CHECK (page_no IS NULL OR page_no > 0),
    CONSTRAINT "FK_notes_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_notes_chapters_book_id_chapter_id" FOREIGN KEY (book_id, chapter_id) REFERENCES chapters (book_id, chapter_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_notes_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE collection_books (
    collection_id bigint NOT NULL,
    book_id bigint NOT NULL,
    sort_order integer NOT NULL DEFAULT (0),
    added_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_collection_books" PRIMARY KEY (collection_id, book_id),
    CONSTRAINT "FK_collection_books_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_collection_books_collections_collection_id" FOREIGN KEY (collection_id) REFERENCES collections (collection_id) ON DELETE RESTRICT
);


CREATE TABLE collection_favorites (
    user_id bigint NOT NULL,
    collection_id bigint NOT NULL,
    favorited_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_collection_favorites" PRIMARY KEY (user_id, collection_id),
    CONSTRAINT "FK_collection_favorites_collections_collection_id" FOREIGN KEY (collection_id) REFERENCES collections (collection_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_collection_favorites_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE collection_members (
    collection_id bigint NOT NULL,
    user_id bigint NOT NULL,
    role character varying(20) NOT NULL DEFAULT ('VIEWER'),
    status character varying(20) NOT NULL DEFAULT ('INVITED'),
    joined_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_collection_members" PRIMARY KEY (collection_id, user_id),
    CONSTRAINT ck_collection_members_0 CHECK (role IN ('VIEWER', 'EDITOR')),
    CONSTRAINT ck_collection_members_1 CHECK (status IN ('INVITED', 'ACCEPTED', 'DECLINED', 'REVOKED')),
    CONSTRAINT "FK_collection_members_collections_collection_id" FOREIGN KEY (collection_id) REFERENCES collections (collection_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_collection_members_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE order_items (
    order_item_id bigint GENERATED ALWAYS AS IDENTITY,
    order_id bigint NOT NULL,
    book_id bigint NOT NULL,
    access_type character varying(20) NOT NULL,
    unit_price numeric(12,2) NOT NULL DEFAULT (0),
    quantity integer NOT NULL DEFAULT (1),
    line_total numeric(12,2) NOT NULL DEFAULT (0),
    rental_days integer,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_order_items" PRIMARY KEY (order_item_id),
    CONSTRAINT ck_order_items_0 CHECK (quantity = 1 AND unit_price >= 0 AND line_total = unit_price),
    CONSTRAINT ck_order_items_1 CHECK ((access_type = 'RENTAL' AND rental_days > 0) OR (access_type = 'PURCHASE' AND rental_days IS NULL)),
    CONSTRAINT ck_order_items_2 CHECK (access_type IN ('PURCHASE', 'RENTAL')),
    CONSTRAINT "FK_order_items_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_order_items_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (order_id) ON DELETE RESTRICT
);


CREATE TABLE reading_progress (
    progress_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    chapter_id bigint,
    current_page integer NOT NULL DEFAULT (0),
    location_token character varying(500),
    status character varying(20) NOT NULL DEFAULT ('NOT_STARTED'),
    started_at timestamp with time zone,
    last_read_at timestamp with time zone,
    finished_at timestamp with time zone,
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_reading_progress" PRIMARY KEY (progress_id),
    CONSTRAINT ck_reading_progress_0 CHECK (current_page >= 0),
    CONSTRAINT ck_reading_progress_1 CHECK (status <> 'FINISHED' OR finished_at IS NOT NULL),
    CONSTRAINT ck_reading_progress_2 CHECK (status IN ('NOT_STARTED', 'READING', 'FINISHED')),
    CONSTRAINT "FK_reading_progress_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reading_progress_chapters_book_id_chapter_id" FOREIGN KEY (book_id, chapter_id) REFERENCES chapters (book_id, chapter_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reading_progress_user_books_user_id_book_id" FOREIGN KEY (user_id, book_id) REFERENCES user_books (user_id, book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reading_progress_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE reading_sessions (
    session_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    chapter_id bigint,
    client_session_key character varying(100) NOT NULL,
    started_at timestamp with time zone NOT NULL,
    ended_at timestamp with time zone,
    active_seconds integer NOT NULL DEFAULT (0),
    start_page integer NOT NULL DEFAULT (0),
    end_page integer NOT NULL DEFAULT (0),
    pages_read integer NOT NULL DEFAULT (0),
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_reading_sessions" PRIMARY KEY (session_id),
    CONSTRAINT ck_reading_sessions_0 CHECK (active_seconds >= 0 AND pages_read >= 0 AND start_page >= 0 AND end_page >= 0),
    CONSTRAINT ck_reading_sessions_1 CHECK (ended_at IS NULL OR (ended_at >= started_at AND active_seconds <= EXTRACT(EPOCH FROM ended_at - started_at))),
    CONSTRAINT "FK_reading_sessions_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reading_sessions_chapters_book_id_chapter_id" FOREIGN KEY (book_id, chapter_id) REFERENCES chapters (book_id, chapter_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reading_sessions_user_books_user_id_book_id" FOREIGN KEY (user_id, book_id) REFERENCES user_books (user_id, book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reading_sessions_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE TABLE book_entitlements (
    entitlement_id bigint GENERATED ALWAYS AS IDENTITY,
    user_id bigint NOT NULL,
    book_id bigint NOT NULL,
    order_item_id bigint,
    subscription_id bigint,
    source_type character varying(20) NOT NULL,
    starts_at timestamp with time zone NOT NULL,
    expires_at timestamp with time zone,
    revoked_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_book_entitlements" PRIMARY KEY (entitlement_id),
    CONSTRAINT ck_book_entitlements_0 CHECK ((source_type IN ('PURCHASE','RENTAL') AND order_item_id IS NOT NULL AND subscription_id IS NULL) OR (source_type = 'PREMIUM' AND subscription_id IS NOT NULL AND order_item_id IS NULL)),
    CONSTRAINT ck_book_entitlements_1 CHECK (expires_at IS NULL OR expires_at > starts_at),
    CONSTRAINT ck_book_entitlements_2 CHECK (source_type IN ('PURCHASE', 'RENTAL', 'PREMIUM')),
    CONSTRAINT "FK_book_entitlements_books_book_id" FOREIGN KEY (book_id) REFERENCES books (book_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_book_entitlements_order_items_order_item_id" FOREIGN KEY (order_item_id) REFERENCES order_items (order_item_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_book_entitlements_user_subscriptions_subscription_id" FOREIGN KEY (subscription_id) REFERENCES user_subscriptions (subscription_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_book_entitlements_users_user_id" FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);


CREATE INDEX "IX_book_authors_author_id" ON book_authors (author_id);


CREATE UNIQUE INDEX "IX_book_categories_book_id" ON book_categories (book_id) WHERE is_primary = TRUE;


CREATE INDEX "IX_book_categories_category_id" ON book_categories (category_id);


CREATE INDEX "IX_book_entitlements_book_id" ON book_entitlements (book_id);


CREATE UNIQUE INDEX "IX_book_entitlements_order_item_id" ON book_entitlements (order_item_id);


CREATE INDEX "IX_book_entitlements_subscription_id" ON book_entitlements (subscription_id);


CREATE INDEX "IX_book_entitlements_user_id" ON book_entitlements (user_id);


CREATE INDEX "IX_book_view_history_book_id" ON book_view_history (book_id);


CREATE UNIQUE INDEX "IX_book_view_history_event_key" ON book_view_history (event_key);


CREATE INDEX "IX_book_view_history_user_id_viewed_at" ON book_view_history (user_id, viewed_at);


CREATE INDEX "IX_bookmarks_book_id_chapter_id" ON bookmarks (book_id, chapter_id);


CREATE UNIQUE INDEX "IX_bookmarks_user_id_book_id_location_token" ON bookmarks (user_id, book_id, location_token) WHERE location_token IS NOT NULL;


CREATE UNIQUE INDEX "IX_books_isbn" ON books (isbn);


CREATE INDEX "IX_categories_parent_category_id" ON categories (parent_category_id);


CREATE UNIQUE INDEX "IX_categories_slug" ON categories (slug);


CREATE UNIQUE INDEX "IX_chapters_book_id_chapter_id" ON chapters (book_id, chapter_id);


CREATE UNIQUE INDEX "IX_chapters_book_id_chapter_no" ON chapters (book_id, chapter_no);


CREATE INDEX "IX_collection_books_book_id" ON collection_books (book_id);


CREATE INDEX "IX_collection_favorites_collection_id" ON collection_favorites (collection_id);


CREATE INDEX "IX_collection_members_user_id" ON collection_members (user_id);


CREATE UNIQUE INDEX "IX_collections_user_id_name" ON collections (user_id, name);


CREATE INDEX "IX_downloads_book_id" ON downloads (book_id);


CREATE UNIQUE INDEX "IX_downloads_user_id_book_id_device_key" ON downloads (user_id, book_id, device_key);


CREATE INDEX "IX_highlights_book_id_chapter_id" ON highlights (book_id, chapter_id);


CREATE INDEX "IX_highlights_user_id" ON highlights (user_id);


CREATE INDEX "IX_notes_book_id_chapter_id" ON notes (book_id, chapter_id);


CREATE INDEX "IX_notes_user_id" ON notes (user_id);


CREATE INDEX "IX_order_items_book_id" ON order_items (book_id);


CREATE UNIQUE INDEX "IX_order_items_order_id_book_id_access_type" ON order_items (order_id, book_id, access_type);


CREATE UNIQUE INDEX "IX_orders_order_number" ON orders (order_number);


CREATE UNIQUE INDEX "IX_orders_payment_reference" ON orders (payment_reference);


CREATE INDEX "IX_orders_user_id" ON orders (user_id);


CREATE UNIQUE INDEX "IX_reading_goals_user_id_target_year" ON reading_goals (user_id, target_year);


CREATE INDEX "IX_reading_progress_book_id_chapter_id" ON reading_progress (book_id, chapter_id);


CREATE UNIQUE INDEX "IX_reading_progress_user_id_book_id" ON reading_progress (user_id, book_id);


CREATE INDEX "IX_reading_sessions_book_id_chapter_id" ON reading_sessions (book_id, chapter_id);


CREATE UNIQUE INDEX "IX_reading_sessions_client_session_key" ON reading_sessions (client_session_key);


CREATE INDEX "IX_reading_sessions_user_id_book_id" ON reading_sessions (user_id, book_id);


CREATE INDEX "IX_reading_sessions_user_id_started_at" ON reading_sessions (user_id, started_at);


CREATE INDEX "IX_reviews_book_id" ON reviews (book_id);


CREATE UNIQUE INDEX "IX_reviews_user_id_book_id" ON reviews (user_id, book_id);


CREATE INDEX "IX_saved_books_book_id" ON saved_books (book_id);


CREATE UNIQUE INDEX "IX_subscription_plans_plan_code" ON subscription_plans (plan_code);


CREATE INDEX "IX_user_books_book_id" ON user_books (book_id);


CREATE UNIQUE INDEX "IX_user_books_user_id_book_id" ON user_books (user_id, book_id);


CREATE INDEX "IX_user_subscriptions_plan_id" ON user_subscriptions (plan_id);


CREATE UNIQUE INDEX "IX_user_subscriptions_provider_reference" ON user_subscriptions (provider_reference);


CREATE UNIQUE INDEX "IX_user_subscriptions_user_id" ON user_subscriptions (user_id) WHERE status = 'ACTIVE';


CREATE UNIQUE INDEX "IX_users_email" ON users (email);



-- Cross-table rules that cannot be expressed as PostgreSQL CHECK constraints.
CREATE FUNCTION booknest_validate_location() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE pages integer;
BEGIN
  SELECT page_count INTO pages FROM books WHERE book_id = NEW.book_id FOR SHARE;
  IF TG_TABLE_NAME = 'chapters' THEN
    IF NEW.end_page > pages THEN RAISE EXCEPTION 'Chapter exceeds book pages' USING ERRCODE = '23514'; END IF;
  ELSIF TG_TABLE_NAME = 'reading_progress' THEN
    IF NEW.current_page > pages THEN RAISE EXCEPTION 'Progress exceeds book pages' USING ERRCODE = '23514'; END IF;
  ELSIF TG_TABLE_NAME = 'reading_sessions' THEN
    IF NEW.start_page > pages OR NEW.end_page > pages THEN RAISE EXCEPTION 'Session exceeds book pages' USING ERRCODE = '23514'; END IF;
  ELSE
    IF NEW.page_no > pages THEN RAISE EXCEPTION 'Annotation exceeds book pages' USING ERRCODE = '23514'; END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER validate_location BEFORE INSERT OR UPDATE ON chapters FOR EACH ROW EXECUTE FUNCTION booknest_validate_location();
CREATE TRIGGER validate_location BEFORE INSERT OR UPDATE ON reading_progress FOR EACH ROW EXECUTE FUNCTION booknest_validate_location();
CREATE TRIGGER validate_location BEFORE INSERT OR UPDATE ON reading_sessions FOR EACH ROW EXECUTE FUNCTION booknest_validate_location();
CREATE TRIGGER validate_location BEFORE INSERT OR UPDATE ON notes FOR EACH ROW EXECUTE FUNCTION booknest_validate_location();
CREATE TRIGGER validate_location BEFORE INSERT OR UPDATE ON bookmarks FOR EACH ROW EXECUTE FUNCTION booknest_validate_location();
CREATE TRIGGER validate_location BEFORE INSERT OR UPDATE ON highlights FOR EACH ROW EXECUTE FUNCTION booknest_validate_location();

CREATE FUNCTION booknest_validate_page_count() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NEW.page_count < OLD.page_count AND (
    EXISTS(SELECT 1 FROM chapters WHERE book_id = NEW.book_id AND end_page > NEW.page_count) OR
    EXISTS(SELECT 1 FROM reading_progress WHERE book_id = NEW.book_id AND current_page > NEW.page_count) OR
    EXISTS(SELECT 1 FROM reading_sessions WHERE book_id = NEW.book_id AND (start_page > NEW.page_count OR end_page > NEW.page_count)) OR
    EXISTS(SELECT 1 FROM notes WHERE book_id = NEW.book_id AND page_no > NEW.page_count) OR
    EXISTS(SELECT 1 FROM bookmarks WHERE book_id = NEW.book_id AND page_no > NEW.page_count) OR
    EXISTS(SELECT 1 FROM highlights WHERE book_id = NEW.book_id AND page_no > NEW.page_count)) THEN
    RAISE EXCEPTION 'Page count would invalidate existing content locations' USING ERRCODE = '23514';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER validate_page_count BEFORE UPDATE ON books FOR EACH ROW EXECUTE FUNCTION booknest_validate_page_count();

CREATE FUNCTION booknest_validate_entitlement() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE source_user bigint; source_book bigint; source_access varchar; paid_status varchar;
        source_start timestamptz; source_end timestamptz;
BEGIN
  IF NEW.source_type IN ('PURCHASE','RENTAL') THEN
    SELECT o.user_id, i.book_id, i.access_type, o.status, o.paid_at,
      CASE WHEN i.access_type = 'RENTAL' THEN o.paid_at + make_interval(days => i.rental_days) ELSE NULL END
    INTO source_user, source_book, source_access, paid_status, source_start, source_end
    FROM order_items i JOIN orders o USING(order_id) WHERE i.order_item_id = NEW.order_item_id;
    IF source_user IS DISTINCT FROM NEW.user_id OR source_book IS DISTINCT FROM NEW.book_id OR
       source_access IS DISTINCT FROM NEW.source_type OR paid_status NOT IN ('PAID','REFUNDED') OR
       NEW.starts_at IS DISTINCT FROM source_start OR NEW.expires_at IS DISTINCT FROM source_end THEN
      RAISE EXCEPTION 'Entitlement does not match paid source' USING ERRCODE = '23514';
    END IF;
  ELSE
    SELECT user_id, started_at, ends_at INTO source_user, source_start, source_end FROM user_subscriptions WHERE subscription_id = NEW.subscription_id;
    IF source_user IS DISTINCT FROM NEW.user_id OR NEW.starts_at < source_start OR NEW.expires_at IS NULL OR NEW.expires_at > source_end THEN
      RAISE EXCEPTION 'Entitlement exceeds subscription source' USING ERRCODE = '23514';
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER validate_entitlement BEFORE INSERT OR UPDATE ON book_entitlements FOR EACH ROW EXECUTE FUNCTION booknest_validate_entitlement();

CREATE FUNCTION booknest_immutable_order_line() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE parent_id bigint; parent_status varchar;
BEGIN
  parent_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.order_id ELSE NEW.order_id END;
  SELECT status INTO parent_status FROM orders WHERE order_id = parent_id FOR UPDATE;
  IF parent_status <> 'PENDING' THEN RAISE EXCEPTION 'Order lines are immutable after checkout completion' USING ERRCODE = '23514'; END IF;
  IF TG_OP = 'UPDATE' AND OLD.order_id <> NEW.order_id THEN RAISE EXCEPTION 'Cannot move an order line' USING ERRCODE = '23514'; END IF;
  IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END $$;
CREATE TRIGGER immutable_order_line BEFORE INSERT OR UPDATE OR DELETE ON order_items FOR EACH ROW EXECUTE FUNCTION booknest_immutable_order_line();

CREATE FUNCTION booknest_validate_paid_order() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE line_amount numeric;
BEGIN
  IF OLD.status IN ('PAID','REFUNDED') AND (NEW.total_amount IS DISTINCT FROM OLD.total_amount OR
     NEW.currency_code IS DISTINCT FROM OLD.currency_code OR NEW.user_id IS DISTINCT FROM OLD.user_id OR
     NEW.payment_reference IS DISTINCT FROM OLD.payment_reference OR NEW.paid_at IS DISTINCT FROM OLD.paid_at) THEN
    RAISE EXCEPTION 'Paid order financial fields are immutable' USING ERRCODE = '23514';
  END IF;
  IF NEW.status = 'PAID' THEN
    SELECT sum(line_total) INTO line_amount FROM order_items WHERE order_id = NEW.order_id;
    IF line_amount IS NULL OR line_amount <> NEW.total_amount OR NEW.payment_reference IS NULL THEN
      RAISE EXCEPTION 'Paid order total or reference invalid' USING ERRCODE = '23514';
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER validate_paid_order BEFORE UPDATE ON orders FOR EACH ROW EXECUTE FUNCTION booknest_validate_paid_order();
