BEGIN;
SELECT pg_advisory_xact_lock(410172029);
INSERT INTO categories(parent_category_id,name,slug,icon_key,sort_order)
SELECT c.category_id,v.name,v.slug,'book-open',v.sort_order FROM (VALUES
('fiction','Fantasy','fiction-fantasy',1),
('fiction','Romance','fiction-romance',2),
('fiction','Mystery','fiction-mystery',3),
('fiction','Science Fiction','fiction-science-fiction',4),
('education','Language','education-language',1),
('education','Science','education-science',2),
('education','History','education-history',3),
('education','Technology','education-technology',4),
('business','Management','business-management',1),
('business','Marketing','business-marketing',2),
('business','Finance','business-finance',3),
('business','Career','business-career',4),
('self-help','Motivation','self-help-motivation',1),
('self-help','Happiness','self-help-happiness',2),
('self-help','Productivity','self-help-productivity',3),
('self-help','Mental Health','self-help-mental-health',4)
) v(parent_slug,name,slug,sort_order) JOIN categories c ON c.slug=v.parent_slug ON CONFLICT(slug) DO NOTHING;
INSERT INTO book_categories(book_id,category_id,is_primary)
SELECT b.book_id,c.category_id,false FROM books b JOIN categories c ON c.slug=CASE b.title
WHEN 'Dune' THEN 'fiction-science-fiction' WHEN 'Project Hail Mary' THEN 'fiction-science-fiction'
WHEN 'The Midnight Library' THEN 'fiction-fantasy' WHEN 'Where the Crawdads Sing' THEN 'fiction-mystery'
WHEN 'The Nightingale' THEN 'fiction-romance' WHEN 'The Book Thief' THEN 'fiction-fantasy'
WHEN 'Atomic Habits' THEN 'self-help-productivity' WHEN 'Deep Work' THEN 'education-technology'
WHEN 'The Psychology of Money' THEN 'business-finance' WHEN 'Rich Dad Poor Dad' THEN 'business-finance'
WHEN 'Ikigai' THEN 'self-help-happiness' WHEN 'Good to Great' THEN 'business-management'
WHEN 'Start with Why' THEN 'business-career' WHEN 'Mindset' THEN 'education-science'
END WHERE b.content_storage_key LIKE 'demo/%' ON CONFLICT DO NOTHING;
COMMIT;
