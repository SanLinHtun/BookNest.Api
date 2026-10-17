-- Figma catalog fixtures. DEMO prices/page counts are UI examples, not licensed ebook content.
BEGIN;
SELECT pg_advisory_xact_lock(410172027);
INSERT INTO categories(name,slug,icon_key,sort_order) VALUES
('Biography','biography','book-open',5),
('History','history','book-open',6),
('Science','science','book-open',7),
('Technology','technology','book-open',8),
('Art & Culture','art-culture','book-open',9),
('Comics','comics','book-open',10),
('Travel','travel','book-open',11),
('Religion','religion','book-open',12),
('Children','children','book-open',13),
('Martial','martial','book-open',14),
('Health','health','book-open',15),
('Cooking','cooking','book-open',16)
ON CONFLICT(slug) DO NOTHING;
CREATE TEMP TABLE figma_catalog(title text,author_name text,category_slug text,price numeric,pages int,slug text) ON COMMIT DROP;
INSERT INTO figma_catalog VALUES
('The Midnight Library','Matt Haig','fiction',1200,304,'the-midnight-library'),
('Where the Crawdads Sing','Delia Owens','fiction',1100,368,'where-the-crawdads-sing'),
('Project Hail Mary','Andy Weir','fiction',1500,496,'project-hail-mary'),
('The Nightingale','Kristin Hannah','fiction',1200,440,'the-nightingale'),
('Dune','Frank Herbert','fiction',1400,896,'dune'),
('The Book Thief','Markus Zusak','fiction',1100,552,'the-book-thief'),
('Deep Work','Cal Newport','education',1200,304,'deep-work'),
('Thinking, Fast and Slow','Daniel Kahneman','education',1300,512,'thinking-fast-and-slow'),
('Mindset','Carol S. Dweck','education',1100,320,'mindset'),
('Range','David Epstein','education',1300,352,'range'),
('The 7 Habits of Highly Effective People','Stephen R. Covey','education',1500,432,'the-7-habits-of-highly-effective-people'),
('The Lean Startup','Eric Ries','business',1300,336,'the-lean-startup'),
('Zero to One','Peter Thiel','business',1400,224,'zero-to-one'),
('Good to Great','Jim Collins','business',1200,320,'good-to-great'),
('The Innovator''s Dilemma','Clayton Christensen','business',1300,288,'the-innovator-s-dilemma'),
('Start with Why','Simon Sinek','business',1400,256,'start-with-why'),
('The Subtle Art of Not Giving a F*ck','Mark Manson','self-help',1200,224,'the-subtle-art-of-not-giving-a-f-ck'),
('Dopamine Nation','Anna Lembke','self-help',1300,304,'dopamine-nation'),
('You Are a Badass','Jen Sincero','self-help',1100,256,'you-are-a-badass'),
('The Whole-Brain Child','Daniel J. Siegel','self-help',1300,192,'the-whole-brain-child'),
('Digital Minimalism','Cal Newport','self-help',1200,304,'digital-minimalism'),
('Sapiens','Yuval Noah Harari','history',1400,464,'sapiens'),
('The Art of Simple Food','Alice Waters','cooking',1100,416,'the-art-of-simple-food'),
('The Creative Act','Rick Rubin','art-culture',1300,432,'the-creative-act'),
('The Travel Book','Lonely Planet','travel',1800,448,'the-travel-book'),
('A Life in Parts','Bryan Cranston','biography',1300,288,'a-life-in-parts'),
('Four Thousand Weeks','Oliver Burkeman','self-help',1200,288,'four-thousand-weeks');
INSERT INTO authors(author_name) SELECT DISTINCT f.author_name FROM figma_catalog f WHERE NOT EXISTS(SELECT 1 FROM authors a WHERE a.author_name=f.author_name);
INSERT INTO books(title,description,language_code,page_count,purchase_price,rental_price,currency_code,rental_days,content_storage_key,file_format,premium_eligible,status,cover_image_url)
SELECT f.title,'DEMO catalog metadata for Figma integration. Prices and page counts are sample values. No licensed ebook content is included.','en',f.pages,f.price,300,'JPY',30,'demo/figma/'||f.slug||'.pdf','PDF',false,'PUBLISHED','https://booknest-api-3jl3.onrender.com/images/figma/catalog-placeholder.svg'
FROM figma_catalog f WHERE NOT EXISTS(SELECT 1 FROM books b WHERE b.content_storage_key='demo/figma/'||f.slug||'.pdf');
INSERT INTO book_authors(book_id,author_id,display_order)
SELECT b.book_id,a.author_id,1 FROM figma_catalog f JOIN books b ON b.content_storage_key='demo/figma/'||f.slug||'.pdf' JOIN authors a ON a.author_name=f.author_name ON CONFLICT DO NOTHING;
INSERT INTO book_categories(book_id,category_id,is_primary)
SELECT b.book_id,c.category_id,true FROM figma_catalog f JOIN books b ON b.content_storage_key='demo/figma/'||f.slug||'.pdf' JOIN categories c ON c.slug=f.category_slug ON CONFLICT DO NOTHING;
INSERT INTO chapters(book_id,chapter_no,title,start_page,end_page,sort_order)
SELECT b.book_id,n,'Demo section '||n,1+((n-1)*b.page_count/4),n*b.page_count/4,n FROM books b CROSS JOIN generate_series(1,4) n
WHERE (b.content_storage_key LIKE 'demo/figma/%' OR b.content_storage_key LIKE 'demo/home/%') AND NOT EXISTS(SELECT 1 FROM chapters c WHERE c.book_id=b.book_id AND c.chapter_no=n);
COMMIT;

