
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
