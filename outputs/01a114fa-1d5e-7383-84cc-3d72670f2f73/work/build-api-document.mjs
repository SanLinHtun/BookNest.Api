import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const work=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(work,'../../..');
const out=path.resolve(work,'..');
const metadata=JSON.parse(await fs.readFile(path.join(work,'metadata.json'),'utf8'));
const openapi=JSON.parse(await fs.readFile(path.join(work,'openapi.json'),'utf8'));
const requests=new Map(metadata.schemas.map(s=>[s.name,s]));
const entities=new Map(metadata.entities.map(s=>[s.name,s]));
const sourceFiles={};
for(const name of await fs.readdir(path.join(root,'BookNest.Api/Controllers'))) {
  if(name.endsWith('.cs')) sourceFiles[name]=await fs.readFile(path.join(root,'BookNest.Api/Controllers',name),'utf8');
}
const groupOrder=['Authentication','Profile','Catalog','Library','Reading','Annotations','Downloads','Collections','Commerce','Payments','Administration','System'];
const domains={AuthController:'Authentication',ProfileController:'Profile',CatalogController:'Catalog',LibraryController:'Library',ReadingController:'Reading',AnnotationsController:'Annotations',DownloadsController:'Downloads',CollectionsController:'Collections',CommerceController:'Commerce',PaymentCallbacksController:'Payments',AdminController:'Administration',System:'System'};
const sourceFile=c=>c==='ProfileController'?'AuthController.cs':c==='PaymentCallbacksController'?'CommerceController.cs':c+'.cs';
const cleanRoute=r=>r.replace(/\{(\w+):\w+\}/g,'{$1}');
const summaries={};
for(const [filename,text] of Object.entries(sourceFiles)) {
  const regex=/\/\/\/ <summary>\s*((?:(?!\/\/\/ <\/summary>)[\s\S])*?)\/\/\/ <\/summary>\s*(?:\[[^\n]*\]\s*)*public (?:async )?Task<IActionResult> (\w+)\(/g;
  for(const match of text.matchAll(regex)) summaries[filename+':'+match[2]]=match[1].replace(/\/\/\//g,'').trim().replace(/\s+/g,' ');
}
// Return shapes below are read from the controller's actual return statements, because most
// IActionResult actions publish only status codes in their generated OpenAPI document.
const shapes={
 AuthController:{Register:'AuthTokens',Login:'AuthTokens',Refresh:'AuthTokens'},
 ProfileController:{Get:'User',Update:'User',Delete:'None'},
 CatalogController:{Books:'BookPage',Detail:'BookDetail',Chapters:'Chapter[]',Categories:'Category[]',Authors:'Author[]',Reviews:'ReviewDisplay[]',Popular:'Book[]',View:'None'},
 LibraryController:{Library:'LibraryPage',Add:'None',Archive:'None',Saved:'SavedItem[]',Save:'None',Unsave:'None',Recent:'RecentlyViewedItem[]',Counts:'LibraryCounts',Review:'Review',DeleteReview:'None'},
 ReadingController:{GetProgress:'ProgressResult',Progress:'ReadingProgress',Session:'ReadingSession',SetGoal:'ReadingGoal',Goal:'GoalResult',Statistics:'ReadingStatistics'},
 AnnotationsController:{List:'Note[] / Bookmark[] / Highlight[]',Create:'Note / Bookmark / Highlight',Update:'Note / Bookmark / Highlight',Delete:'None'},
 DownloadsController:{List:'Download[]',Update:'Download',Access:'BookAccess',Content:'Binary PDF / EPUB'},
 CollectionsController:{List:'Collection[]',Detail:'CollectionDetail',Create:'Collection',Update:'Collection',Delete:'None',AddBook:'None',RemoveBook:'None',Favorite:'None',Unfavorite:'None',Members:'CollectionMember[]',Invite:'CollectionMember',Respond:'CollectionMember',Revoke:'None',Invitations:'CollectionMember[]'},
 CommerceController:{Checkout:'Order',Orders:'Order[]',Order:'OrderDetail',CancelOrder:'None',Plans:'SubscriptionPlan[]',Subscriptions:'UserSubscription[]',Subscribe:'UserSubscription',CancelSubscription:'UserSubscription',Entitlements:'BookEntitlement[]'},
 PaymentCallbacksController:{Callback:'Order / UserSubscription'},
 AdminController:{Books:'Book[]',CreateBook:'Book',UpdateBook:'Book',ArchiveBook:'None',CreateAuthor:'Author',UpdateAuthor:'Author',CreateCategory:'Category',UpdateCategory:'Category',CreateChapter:'Chapter',UpdateChapter:'Chapter',LinkAuthor:'None',UnlinkAuthor:'None',LinkCategory:'None',UnlinkCategory:'None',CreatePlan:'SubscriptionPlan',UpdatePlan:'SubscriptionPlan',ModerateReview:'Review'},
 System:{Health:'HealthResult'}
};
const custom={
 AuthTokens:[['tokenType','String','Bearer'],['accessToken','String','Opaque access token'],['expiresIn','Int32','Lifetime in seconds (3600)'],['refreshToken','String','Opaque refresh token']],
 BookPage:[['page','Int32','Requested page'],['pageSize','Int32','Requested page size'],['total','Int32','Matching books before pagination'],['items','Book[]','Published books']],
 BookDetail:[['book','Book','Published catalog book'],['authors','Author[]','Ordered by authorship displayOrder'],['categories','Category[]','Active linked categories'],['rating','Double?','Average PUBLISHED rating; null without published reviews'],['reviewCount','Int32','Count of published reviews']],
 LibraryPage:[['page','Int32','Requested page'],['pageSize','Int32','Requested page size'],['total','Int32','Matching active library entries'],['items','LibraryItem[]','Book and current progress']],
 LibraryItem:[['book','Book','Catalog metadata'],['addedAt','DateTimeOffset','When library membership was added'],['progress','ReadingProgress?','Current reading state, or null']],
 SavedItem:[['book','Book','Saved catalog metadata'],['savedAt','DateTimeOffset','When the book was saved']],
 RecentlyViewedItem:[['book','Book','Published catalog metadata'],['viewedAt','DateTimeOffset?','Latest recorded view for this user/book']],
 ReviewDisplay:[['reviewId','Int64','Review identifier'],['rating','Int32','Stars, 1–5'],['reviewText','String?','Optional review text'],['createdAt','DateTimeOffset','Review creation time'],['displayName','String','Reviewer display name'],['avatarUrl','String?','Reviewer avatar']],
 LibraryCounts:[['totalBooks','Int32','Active library memberships'],['readingNow','Int32','Active library books marked READING'],['finished','Int32','Active library books marked FINISHED'],['saved','Int32','Saved-book count'],['downloads','Int32','Distinct completed, unexpired books on deviceKey; 0 without deviceKey']],
 ProgressResult:[['progress','ReadingProgress','Saved position and state'],['percent','Decimal','Rounded 100 × currentPage / pageCount, capped at 100']],
 GoalResult:[['goal','ReadingGoal?','Annual goal, or null when not configured'],['finishedBooks','Int32','FINISHED books whose finishedAt falls in the local target year']],
 ReadingStatistics:[['period','String','week / month / year / all'],['timezone','String','User-local timezone identifier'],['start','DateOnly','First local date of selected period'],['end','DateOnly','Today in the user timezone'],['totalSeconds','Double','Active reading seconds in the period'],['daysRead','Int32','Distinct active local dates'],['averageSeconds','Double','Seconds divided by elapsed calendar days'],['pagesRead','Int32','Measured page turns, including rereads'],['longestStreak','Int32','Longest sequence of active local dates'],['percentageChange','Double?','Equal-elapsed previous period comparison; null for zero prior or all time'],['breakdown','ReadingBucket[]','Period chart buckets']],
 ReadingBucket:[['label','String','Local date, week-start date, month or year label'],['seconds','Double','Active seconds in the bucket'],['pages','Int32','Measured page turns in the bucket']],
 CollectionDetail:[['collection','Collection','Accessible collection metadata'],['books','Book[]','Collection books sorted by sortOrder and bookId']],
 OrderDetail:[['order','Order','Owned order'],['items','OrderItem[]','Historical price and access-term snapshots']],
 BookAccess:[['bookId','Int64','Book identifier'],['expiresAt','DateTimeOffset?','Access expiry; null for permanent purchase'],['offlineLicenseExpiresAt','DateTimeOffset','Renew within 7 days or at earlier source expiry'],['contentVersion','Int32','Version used for offline invalidation'],['fileFormat','String','PDF / EPUB'],['fileSizeBytes','Int64?','Known file size in bytes'],['contentUrl','String','Authenticated relative content endpoint; not a public signed URL']],
 HealthResult:[['status','String','ok']],
 ProblemDetails:[['type','String?','Problem type URI, when supplied'],['title','String?','Safe error description'],['status','Int32?','HTTP status'],['detail','String?','Additional explanation, when supplied'],['instance','String?','Request instance, when supplied']],
 ValidationProblemDetails:[['type','String?','Problem type URI'],['title','String?','Validation error summary'],['status','Int32?','Normally 400'],['errors','Object','Field names mapped to arrays of messages']],
};
const noteMap={
 'AuthController.Register':'Email is normalized lowercase; password is hashed. Rate limit: 20 requests/minute per IP.',
 'AuthController.Login':'Only ACTIVE accounts can log in. Admin claims are assigned from server configuration.',
 'AuthController.Refresh':'Refresh token lifetime: 14 days. Tokens are opaque, not JWT. Deleted/suspended users cannot refresh.',
 'ProfileController.Update':'Email is not editable. Timezone must resolve through TimeZoneInfo.',
 'ProfileController.Delete':'Soft deletion sets status DELETED. Existing tokens are rejected on later requests.',
 'CatalogController.Books':'Only PUBLISHED books. Title search uses Contains; categoryId filters linked books.',
 'CatalogController.Detail':'Ratings use only PUBLISHED reviews. Password hashes and contentStorageKey are excluded from JSON.',
 'CatalogController.View':'eventKey is globally unique. Same user/book retries succeed; conflicting reuse returns 409.',
 'LibraryController.Add':'Library membership alone does not grant paid content access.',
 'LibraryController.Save':'Wishlist membership can exist outside the library; it grants no reading access.',
 'LibraryController.Archive':'Archives membership; reading history and purchased access are retained.',
 'LibraryController.Review':'One review per user/book. Existing moderation status is retained when edited.',
 'ReadingController.Progress':'Requires valid content access; validates page/chapter, restores library membership and maintains finishedAt.',
 'ReadingController.Session':'Use unique clientSessionKey. UTC whole-second contiguous active intervals, >0 to 60 seconds. Duplicate retry returns existing record; overlapping activity is rejected.',
 'ReadingController.Goal':'Year must be 1–9998. Count uses user-local year boundaries.',
 'ReadingController.SetGoal':'Year must be 1–9998; targetBooks must be greater than zero.',
 'ReadingController.Statistics':'Monday weeks. Active time splits at local midnight; page turns are attributed to heartbeat completion day. Month buckets start from month day 1.',
 'AnnotationsController.List':'kind selects notes, bookmarks or highlights. Results always belong to the authenticated user.',
 'AnnotationsController.Create':'Requires content access. pageNo or nonblank locationToken required; notes/highlights require text; highlights require both anchors.',
 'AnnotationsController.Update':'Same validation as create; the annotation must belong to the authenticated user.',
 'AnnotationsController.Delete':'Owner-only deletion. Returns 404 for another user\'s annotation.',
 'DownloadsController.Update':'Offline access checked except for REMOVED. Known file size constrains bytesDownloaded; COMPLETED requires full size. License capped at 7 days or source expiry.',
 'DownloadsController.Access':'Offline=true requires plan offlineAccess. Returns license renewal deadline and relative authenticated content URL.',
 'DownloadsController.Content':'Requires valid access and configured private storage. Supports HTTP byte ranges. No file is returned if storage is unconfigured.',
 'CollectionsController.List':'tab = owned / shared / favorites / public. Favorites never grant collection access.',
 'CollectionsController.AddBook':'Owner or accepted SHARED editor may change membership. Updates collection updatedAt.',
 'CollectionsController.RemoveBook':'Owner or accepted SHARED editor may remove a book. Updates collection updatedAt.',
 'CollectionsController.Invite':'Owner-only; collection must be SHARED. Target user must be ACTIVE and cannot be the owner.',
 'CollectionsController.Respond':'Only a pending INVITED membership can be ACCEPTED or DECLINED.',
 'CollectionsController.Revoke':'Owner-only. Sets member status REVOKED; personal reading data remains private.',
 'CommerceController.Checkout':'Server prices and currency only. One line per book, 1–100 items, same currency. Order remains PENDING until verified callback.',
 'CommerceController.CancelOrder':'Only a PENDING owned order can be cancelled.',
 'CommerceController.Subscribe':'Creates PENDING contract; payment provider checkout is not connected.',
 'CommerceController.CancelSubscription':'ACTIVE cancellation sets cancelAtPeriodEnd; paid access lasts through endsAt.',
 'CommerceController.Entitlements':'Historical grants include revoked/expired records. Use books/{bookId}/access to check current effective access.',
 'PaymentCallbacksController.Callback':'Trusted internal adapter only. HMAC-SHA256 over exact UTF-8 body with X-BookNest-Signature. Not a direct provider/store webhook. Duplicate successful confirmations are idempotent.',
 'AdminController.UpdateBook':'Page count cannot invalidate stored locations. Increase contentVersion when replacing content storage key or file format.',
 'AdminController.CreateBook':'contentStorageKey must be a relative path inside private content storage.',
 'AdminController.ArchiveBook':'Sets status ARCHIVED; keeps purchase history and foreign-key references.',
 'AdminController.CreateCategory':'Parent must exist. Category hierarchy cycles are prevented transactionally.',
 'AdminController.UpdateCategory':'Parent must exist. Category hierarchy cycles are prevented transactionally.',
 'AdminController.CreateChapter':'Chapter range must satisfy 1 <= startPage <= endPage <= book.pageCount.',
 'AdminController.UpdateChapter':'Chapter must belong to the specified book; page range must be valid.',
 'AdminController.LinkCategory':'At most one primary category per book; updates run in a transaction.',
 'AdminController.UpdatePlan':'Billing price, currency and interval are immutable here; create a new plan to change billing terms.',
 'AdminController.ModerateReview':'Only PUBLISHED ratings contribute to catalog aggregates.',
};
const explicitErrors={
 'AuthController.Register':[409,429],'AuthController.Login':[401,429],'AuthController.Refresh':[401,429],
 'ProfileController.Update':[400],
 'CatalogController.Detail':[404],'CatalogController.Chapters':[404],'CatalogController.View':[404,409],
 'LibraryController.Add':[404],'LibraryController.Save':[404],'LibraryController.Review':[404],
 'ReadingController.GetProgress':[404],'ReadingController.Progress':[400,403,404],'ReadingController.Session':[400,403,404,409],
 'AnnotationsController.List':[404],'AnnotationsController.Create':[400,403,404],'AnnotationsController.Update':[400,403,404],'AnnotationsController.Delete':[404],
 'DownloadsController.Update':[400,403,404],'DownloadsController.Access':[403,404],'DownloadsController.Content':[403,404,503],
 'CommerceController.Order':[404],'CommerceController.CancelOrder':[409],'CommerceController.Subscribe':[404],'CommerceController.CancelSubscription':[400,404],
 'PaymentCallbacksController.Callback':[400,401,404,409,503],
};
const eps=metadata.endpoints.concat([{controller:'System',action:'Health',method:'GET',route:'/health',auth:'Public',parameters:[]}])
 .sort((a,b)=>groupOrder.indexOf(domains[a.controller])-groupOrder.indexOf(domains[b.controller]) || cleanRoute(a.route).localeCompare(cleanRoute(b.route)) || ['GET','POST','PUT','DELETE'].indexOf(a.method)-['GET','POST','PUT','DELETE'].indexOf(b.method));
const documented=new Set(Object.entries(openapi.paths).flatMap(([r,ops])=>Object.keys(ops).filter(v=>['get','post','put','delete'].includes(v)).map(v=>v.toUpperCase()+' '+r)));
if(eps.length!==documented.size || eps.some(e=>!documented.has(e.method+' '+cleanRoute(e.route))))throw Error('Route inventory differs from running OpenAPI.');
for(let i=0;i<eps.length;i++) {
 const e=eps[i];e.id='API-'+String(i+1).padStart(3,'0');e.domain=domains[e.controller];e.url=cleanRoute(e.route);
 e.shape=shapes[e.controller]?.[e.action];if(!e.shape)throw Error('Missing response '+e.controller+'.'+e.action);
 e.body=e.parameters.find(p=>requests.has(p.type))?.type??'None';if(e.controller==='PaymentCallbacksController')e.body='PaymentCallback';
 e.summary=e.controller==='System'?'Reports process availability without checking database connectivity.':summaries[sourceFile(e.controller)+':'+e.action];
 if(!e.summary || e.summary.includes('</summary>') || e.summary.length>240)throw Error('Invalid purpose '+e.controller+'.'+e.action);
 e.success=e.shape==='None'?'204':e.controller==='AuthController'?'200':e.controller==='DownloadsController'&&e.action==='Content'?'200 / 206':
   e.controller==='CollectionsController'&&e.action==='Create' || e.controller==='CommerceController'&&e.action==='Checkout'?'201':'200';
 if(e.controller==='PaymentCallbacksController')e.auth='Adapter HMAC signature';
 const errors=new Set(explicitErrors[e.controller+'.'+e.action]??[]);
 if(e.parameters.length || e.body!=='None')errors.add(400);
 if(e.auth.includes('bearer'))errors.add(401);if(e.auth.startsWith('Admin'))errors.add(403);
 if(e.controller==='CollectionsController' && !['List','Create','Invitations','Unfavorite'].includes(e.action))errors.add(404);
 if(e.controller==='AdminController' && ['UpdateBook','ArchiveBook','UpdateAuthor','UpdateCategory','CreateChapter','UpdateChapter','UpdatePlan','ModerateReview'].includes(e.action))errors.add(404);
 const uniqueWrites={AdminController:['CreateBook','UpdateBook','CreateCategory','UpdateCategory','CreateChapter','UpdateChapter','LinkAuthor','LinkCategory','CreatePlan'],
   LibraryController:['Add','Save','Review'],ReadingController:['Progress','Session','SetGoal'],AnnotationsController:['Create','Update'],DownloadsController:['Update'],
   CollectionsController:['Create','Update','AddBook','Favorite','Invite'],CommerceController:['Checkout','Subscribe']};
 if(uniqueWrites[e.controller]?.includes(e.action))errors.add(409);
 e.errors=[...errors].sort().join(', ')||'Global errors only';
 e.note=noteMap[e.controller+'.'+e.action]??(e.domain==='Administration'?'Requires an admin claim assigned by the server.':e.auth==='User bearer'?'User-owned data is scoped to the authenticated user.':'');
 e.source=e.controller==='System'?'Program.cs':'Controllers/'+sourceFile(e.controller)+' :: '+e.action;
}

const types={Int32:'integer (32-bit)',Int64:'integer (64-bit)',Decimal:'number (decimal)',Double:'number (double)',Boolean:'boolean',String:'string',DateTimeOffset:'string (UTC date-time)',DateOnly:'string (date)'};
function jsonType(t){const nullable=t.endsWith('?');const raw=t.replace(/\?$/,'');return (types[raw]??raw)+(nullable?' or null':'');}
const examples={email:'reader@example.com',password:'ExamplePassword123!',displayName:'Reader',avatarUrl:null,bio:null,timezone:'Asia/Tokyo',
 title:'Sample Book',description:'Sample description',coverImageUrl:null,isbn:null,publisherName:null,publishedDate:null,languageCode:'en',
 pageCount:100,purchasePrice:1000,rentalPrice:100,currencyCode:'JPY',rentalDays:30,previewUrl:null,contentStorageKey:'books/sample.pdf',
 fileFormat:'PDF',fileSizeBytes:1024000,contentVersion:1,premiumEligible:true,authorName:'Sample Author',name:'Sample Collection',slug:'fiction',
 parentCategoryId:null,iconKey:null,bannerImageUrl:null,sortOrder:0,isActive:true,chapterNo:1,startPage:0,endPage:2,displayOrder:1,isPrimary:true,
 currentPage:10,chapterId:null,locationToken:null,pageNo:10,text:'Remember this passage.',startAnchor:'page:10:start',endAnchor:'page:10:end',
 colorCode:'YELLOW',deviceKey:'ios-device-001',bytesDownloaded:1024000,rating:5,reviewText:'An engaging read.',visibility:'PRIVATE',
 role:'VIEWER',targetBooks:12,planCode:'PREMIUM-MONTH',price:200,billingInterval:'MONTH',offlineAccess:true,adFree:true,
 accessType:'PURCHASE',clientSessionKey:'session-20261007-001',startedAt:'2026-10-07T00:00:00Z',endedAt:'2026-10-07T00:00:30Z',activeSeconds:30,pagesRead:2,
 refreshToken:'<REFRESH_TOKEN>',eventKey:'view-20261007-001',event:'ORDER_PAID',resourceId:1,reference:'verified-provider-reference',amount:1000};
const descriptions={
 bookId:'Book identifier',userId:'Target user identifier; caller identity comes from the token',id:'Resource identifier in the URL',
 authorId:'Author identifier',categoryId:'Category identifier',memberId:'Collection member user identifier',planId:'Subscription plan identifier',
 page:'Pagination starts at 1',pageSize:'Number of records to return, from 1 to 100',search:'Optional title substring',
 kind:'Annotation type: notes, bookmarks or highlights',tab:'Collection tab: owned, shared, favorites or public',
 status:'Status code; accepted values depend on the endpoint/model',period:'Statistics period: week, month, year or all',
 year:'Target local calendar year, from 1 to 9998',offline:'When true, checks offline eligibility',deviceKey:'Client device identifier; never a local file path',
 email:'Normalized lowercase login email',password:'Plaintext input only; server stores a password hash',displayName:'Public profile/reviewer display name',
 timezone:'Timezone for reading statistics and calendar boundaries',contentStorageKey:'Admin-only private relative storage path; never returned in API JSON',
 currentPage:'Current page position, from 0 through book page count',pageNo:'Annotation page; pageNo or locationToken required',
 locationToken:'Reading/annotation anchor token',chapterId:'Optional chapter belonging to the selected book',
 text:'Note text or highlighted passage; required for notes/highlights',startAnchor:'Required for highlights',endAnchor:'Required for highlights',
 clientSessionKey:'Globally unique key for idempotent session retries',startedAt:'UTC beginning of contiguous active reading interval',endedAt:'UTC end of contiguous active interval',
 activeSeconds:'Whole active seconds; equals interval duration, at most 60',pagesRead:'Measured page turns, including rereads',
 reference:'Verified stable provider payment reference',resourceId:'Order or subscription identifier, selected by event',
 amount:'Verified full payment/refund amount',currencyCode:'Currency code',event:'Trusted adapter payment event',
 items:'Checkout lines; 1–100 items, one line per book, same currency',rating:'Star rating, 1 through 5',
 isPrimary:'Only one primary category may be assigned per book',contentVersion:'Increase when replacing book content',
};
function exampleValue(f,schemaName='') {
 const t=f.type.replace(/\?$/,'');
 if(t.endsWith('[]'))return requests.has(t.slice(0,-2))?[requestExample(t.slice(0,-2))]:[];
 if(f.name==='status')return schemaName==='BookRequest'?'PUBLISHED':schemaName==='ProgressRequest'?'READING':schemaName==='MembershipRequest'?'ACCEPTED':schemaName==='ReviewModeration'?'PUBLISHED':schemaName==='DownloadRequest'?'COMPLETED':'ACTIVE';
 if(f.name==='startPage' && schemaName==='ChapterRequest')return 1;
 if(f.name==='endPage' && schemaName==='ChapterRequest')return 20;
 if(f.name==='role')return 'VIEWER';
 if(f.name==='userId')return 2;
 if(f.name==='title' && schemaName==='AnnotationRequest')return 'My note';
 if(f.name==='name' && schemaName==='PlanRequest')return 'Premium Monthly';
 if(Object.hasOwn(examples,f.name))return examples[f.name];
 if(f.nullable)return null;
 if(t==='Boolean')return true;
 if(['Int64','Int32','Decimal','Double'].includes(t))return 1;
 if(t==='DateTimeOffset')return '2026-10-07T00:00:00Z';
 if(t==='DateOnly')return '2026-10-07';
 return 'Example';
}
function requestExample(name){return Object.fromEntries(requests.get(name).fields.map(f=>[f.name,exampleValue(f,name)]));}
function schemaSample(name) {
 if(name==='None')return null;
 if(name==='Binary PDF / EPUB')return '<binary PDF or EPUB bytes>';
 if(name.includes(' / '))return schemaSample(name.split(' / ')[0]);
 if(name.endsWith('[]'))return [schemaSample(name.slice(0,-2))];
 if(entities.has(name))return Object.fromEntries(entities.get(name).fields.map(f=>[f.name,exampleEntity(f,name)]));
 const special={AuthTokens:{tokenType:'Bearer',accessToken:'<ACCESS_TOKEN>',expiresIn:3600,refreshToken:'<REFRESH_TOKEN>'},HealthResult:{status:'ok'},
   ReadingStatistics:{period:'week',timezone:'Asia/Tokyo',start:'2026-10-05',end:'2026-10-07',totalSeconds:30,daysRead:1,averageSeconds:10,pagesRead:2,longestStreak:1,percentageChange:null,breakdown:[{label:'2026-10-07',seconds:30,pages:2}]}};
 if(special[name])return special[name];
 return Object.fromEntries(custom[name].map(([field,type])=>{
   const raw=type.replace(/\?$/,'');
   if(raw==='DateOnly')return [field,field==='start'?'2026-10-05':'2026-10-07'];
   if(raw==='DateTimeOffset')return [field,field==='offlineLicenseExpiresAt'?'2026-10-14T00:00:00Z':'2026-10-07T00:00:00Z'];
   if(type.endsWith('?'))return [field,null];
   if(custom[raw] || entities.has(raw) || raw.endsWith('[]'))return [field,schemaSample(raw)];
   if(field==='contentUrl')return [field,'/api/v1/books/1/content?offline=false'];
   if(field==='period')return [field,'week'];if(field==='timezone')return [field,'Asia/Tokyo'];
   if(field==='label')return [field,'2026-10-07'];
   if(field==='pageSize')return [field,20];if(field==='percent')return [field,10];
   return [field,exampleValue({name:field,type,nullable:false},name)];
 }));
}
function exampleEntity(f,name){
 if(name==='OrderItem' && f.name==='rentalDays')return null;
 if(name==='BookEntitlement' && f.name==='orderItemId')return 1;
 if(name==='UserSubscription' && f.name==='endsAt')return '2026-11-07T00:00:00Z';
 if(name==='Download' && f.name==='downloadedAt')return '2026-10-07T00:00:00Z';
 if(name==='Download' && f.name==='accessExpiresAt')return '2026-10-14T00:00:00Z';
 if(f.name==='status')return ({Book:'PUBLISHED',User:'ACTIVE',ReadingProgress:'READING',Download:'COMPLETED',Review:'PUBLISHED',Order:'PENDING',UserSubscription:'PENDING',CollectionMember:'INVITED'})[name]??'ACTIVE';
 if(f.name==='orderNumber')return 'BN-example-order';if(f.name==='noteText')return 'Remember this passage.';
 if(f.name==='highlightedText')return 'A highlighted passage.';if(f.name==='targetYear')return 2026;
 if(f.name==='unitPrice' || f.name==='lineTotal' || f.name==='totalAmount')return 1000;
 if(f.name==='sourceType')return 'PURCHASE';if(f.name==='cancelAtPeriodEnd')return false;
 if(f.name==='expiresAt' || f.name==='revokedAt' || f.name==='paidAt' || f.name==='archivedAt' || f.name==='finishedAt' || f.name==='paymentReference' || f.name==='providerReference')return null;
 return exampleValue(f,name);
}
const apiRows=eps.map(e=>[e.id,e.domain,e.method,e.url,e.auth,e.summary,e.body,e.success,e.shape,e.errors,e.note,e.source]);
const requestRows=[];
for(const e of eps){
 for(const p of e.parameters.filter(p=>!requests.has(p.type))){
   const where=e.url.includes('{'+p.name+'}')?'Path':'Query';
   const valid=p.name==='page'?'1–100000':p.name==='pageSize'?'1–100':p.name==='kind'?'notes / bookmarks / highlights':p.name==='year'?'1–9998':p.name==='tab'?'owned / shared / favorites / public':p.name==='period'?'week / month / year / all':p.name==='status' && e.controller==='LibraryController'?'NOT_STARTED / READING / FINISHED':p.validation;
   requestRows.push([e.id,e.method+' '+e.url,where,p.name,jsonType(p.type),where==='Path'||p.required?'Yes':p.nullable||p.hasDefault?'No':'Non-null value',p.hasDefault?String(p.defaultValue):'None',valid||'Type validation',descriptions[p.name]??'Endpoint parameter',JSON.stringify(p.hasDefault?p.defaultValue:exampleValue(p))]);
 }
 if(e.auth.includes('bearer'))requestRows.push([e.id,e.method+' '+e.url,'Header','Authorization','string','Yes','None','Bearer <ACCESS_TOKEN>','Authenticated account; admin claim required for admin endpoints','Bearer <ACCESS_TOKEN>']);
 if(e.controller==='PaymentCallbacksController')requestRows.push([e.id,e.method+' '+e.url,'Header','X-BookNest-Signature','string','Yes','None','Hex HMAC-SHA256 over exact UTF-8 body','Generated only by the trusted payment adapter','<HEX_HMAC_SHA256>']);
 if(e.body!=='None'){
   function addFields(name,prefix=''){
     for(const f of requests.get(name).fields){
       const field=prefix+f.name;
       const required=f.required?'Yes':f.nullable||f.hasDefault?'No':'Non-null value';
       requestRows.push([e.id,e.method+' '+e.url,'JSON body',field,jsonType(f.type),required,f.hasDefault?JSON.stringify(f.defaultValue):'None',f.validation||'Type validation',descriptions[f.name]??f.name.replace(/([A-Z])/g,' $1').replace(/^./,s=>s.toUpperCase()),JSON.stringify(exampleValue(f,name))]);
       if(f.type.endsWith('[]') && requests.has(f.type.slice(0,-2)))addFields(f.type.slice(0,-2),field+'[].');
     }
   }addFields(e.body);
 }
}
const responseRows=[];
const entityReference=new Map();
const wbSource='C:/SLH/BookNest/DB/Design/BookNest_DB_Design.xlsx';
// Entity field descriptions come from names and response behavior; schema nullability is reflected from C#.
const usedModels=new Set();
function collectModels(name){
 for(const part of name.split(' / ')){
   const raw=part.replace(/\[\]|\?/g,'');if(usedModels.has(raw))continue;usedModels.add(raw);
   if(custom[raw])for(const [,type] of custom[raw])collectModels(type);
 }
}
eps.forEach(e=>collectModels(e.shape));
for(const s of metadata.entities.filter(s=>usedModels.has(s.name)))for(const f of s.fields){
 const used=eps.filter(e=>e.shape.split(' / ').some(x=>x.replace(/\[\]$/,'')===s.name) || custom[e.shape]?.some(x=>x[1].replace(/\?|\[\]/g,'')===s.name)).map(e=>e.id).join(', ');
 responseRows.push([s.name,f.name,jsonType(f.type),f.nullable?'Yes':'No',descriptions[f.name]??f.name.replace(/([A-Z])/g,' $1').replace(/^./,s=>s.toUpperCase()),JSON.stringify(exampleEntity(f,s.name)),used||'Nested or supporting entity model','Domain/'+s.name+'.cs']);
}
for(const [name,fields] of Object.entries(custom))for(const [field,type,description] of fields){
 const use=eps.filter(e=>e.shape===name).map(e=>e.id).join(', ');
 responseRows.push([name,field,jsonType(type),type.endsWith('?')?'Yes':'No',description,'See Examples',use||'Nested model or shared error contract','Controller return shape / ApiSupport.cs']);
}
const exampleRows=eps.map(e=>{
 const url=e.url.replace(/\{(\w+)\}/g,(_,n)=>n==='kind'?'notes':n==='year'?'2026':'1');
 const query=Object.fromEntries(e.parameters.filter(p=>!requests.has(p.type) && !e.url.includes('{'+p.name+'}')).map(p=>[p.name,p.hasDefault?p.defaultValue:p.nullable?null:exampleValue(p)]).filter(([k,v])=>v!==null));
 const headers=e.auth.includes('bearer')?'Authorization: Bearer <ACCESS_TOKEN>':e.auth==='Adapter HMAC signature'?'X-BookNest-Signature: <HEX_HMAC_SHA256>':'None';
 const response=schemaSample(e.shape);
 if(e.controller==='PaymentCallbacksController'){response.status='PAID';response.paymentReference='verified-provider-reference';response.paidAt='2026-10-07T00:00:00Z';}
 if(e.controller==='CommerceController' && e.action==='CancelSubscription'){response.status='CANCELLED';response.cancelAtPeriodEnd=true;}
 return [e.id,e.method,url,headers,Object.keys(query).length?JSON.stringify(query):'None',e.body==='None'?'None':JSON.stringify(requestExample(e.body)),e.success,e.shape==='None'?'No response body':JSON.stringify(response),
 e.controller==='AnnotationsController'?'Example uses kind=notes; bookmarks and highlights return their corresponding entity.':e.controller==='PaymentCallbacksController'?'ORDER_PAID example. SUBSCRIPTION_PAID returns UserSubscription; refund returns Order.':e.shape==='Binary PDF / EPUB'?'Binary body, not JSON. Range requests may return 206.':'Illustrative values and IDs; not seeded data.'];
});

const ruleRows=[
 ['Transport','API base path','/api/v1','Health is /health; development OpenAPI is /openapi/v1.json.','Program.cs'],
 ['Transport','Local HTTPS base URL','https://localhost:7099','Use a reachable deployment URL from a physical device.','Properties/launchSettings.json'],
 ['Transport','JSON naming','camelCase','IDs are JSON numbers; map to Swift Int64 and Kotlin Long.','ASP.NET JSON defaults; Domain/*.cs'],
 ['Transport','Dates and timestamps','yyyy-MM-dd; UTC ISO 8601','Reading heartbeat timestamps must have UTC offset zero.','ReadingController.cs :: Session'],
 ['Validation','Body fields','Non-null value','Non-null numeric/bool body fields may default to 0/false when omitted. Required validation is listed separately. Send all needed fields; schema/DB rules may reject defaults.','Request record attributes'],
 ['Pagination','Query limits','page 1–100000; pageSize 1–100','Default page=1, pageSize=20. Only catalog Books and Library return page/total envelopes; other lists return arrays.','ApiSupport.cs :: Page; controller return statements'],
 ['Security','Bearer header','Authorization: Bearer <ACCESS_TOKEN>','User identity is taken from the token, not a caller-selected user ID.','Program.cs; ApiSupport.cs :: UserController'],
 ['Security','Access-token duration','3600 seconds','Opaque ASP.NET bearer token; not JWT.','Program.cs :: AddBearerToken'],
 ['Security','Refresh-token duration','14 days','Refresh endpoint checks account is ACTIVE. Store refresh tokens in Keychain/encrypted private storage.','Program.cs; AuthController.cs :: Refresh'],
 ['Security','Admin access','booknest_admin=true claim','Assigned only from server AdminUserIds configuration. Register/login cannot request this claim.','AuthController.cs :: Principal'],
 ['Security','Auth throttling','20 requests per minute per IP','Applies to register/login/refresh; excess requests return 429.','Program.cs :: AddRateLimiter'],
 ['Security','Private fields','passwordHash; contentStorageKey','Excluded from all serialized entity responses with JsonIgnore.','Domain/User.cs; Domain/Book.cs'],
 ['Security','Collection sharing','Accepted membership only','VIEWER can read; EDITOR can manage books; owner manages invitations and metadata. Favorite never grants access.','CollectionAccess.cs; CollectionsController.cs'],
 ['Content','Access sources','PURCHASE / RENTAL / PREMIUM','Saving or adding a book to the library grants no paid-content access. Valid source and time bounds are checked.','ApiSupport.cs :: AccessService'],
 ['Content','Offline license','Renew within 7 days','Expiry is capped at earlier source expiry. Premium offline access also requires the plan offlineAccess benefit. Device must enforce cached expiry locally.','DownloadsController.cs'],
 ['Content','Content stream','application/pdf or application/epub+zip','Private storage must be configured. Supports range responses; content URL requires bearer authentication.','DownloadsController.cs :: Content'],
 ['Reading','Heartbeat','>0 to 60 seconds','Contiguous active interval, whole-second UTC times; omit idle time. clientSessionKey permits exact retries; overlaps rejected across devices/books.','ReadingController.cs :: Session'],
 ['Reading','Statistics','week / month / year / all','Weeks start Monday; month chart uses 7-day buckets from month day 1; year groups by month; all time by year.','ReadingStatistics.cs :: Calculate'],
 ['Reading','Averages and comparison','Elapsed calendar days','Average includes today. Comparison uses equal elapsed prior period; prior zero and all-time comparison return null.','ReadingStatistics.cs :: Calculate'],
 ['Reading','Pages and streak','Measured turns and active dates','Seconds split at local midnight. Page turns assigned to interval completion day. Rereads count; streak uses consecutive active local dates.','ReadingStatistics.cs :: Calculate'],
 ['Payments','Callback contract','Trusted internal adapter','Provider/Apple/Google verification must occur in a trusted adapter before signing this API callback. Provider integration is not implemented.','PaymentService.cs; README.md'],
 ['Payments','Signature','Hex HMAC-SHA256','Header X-BookNest-Signature; sign exact UTF-8 JSON bytes using a server-only secret of at least 32 bytes.','PaymentService.cs :: Verify'],
 ['Payments','Supported events','ORDER_PAID / ORDER_REFUNDED / SUBSCRIPTION_PAID','Order/refund confirmations must match stored amount and currency. Refund uses original provider reference and full paid amount.','CommerceController.cs :: PaymentCallback; PaymentService.cs'],
 ['Payments','Paid order','Transactional and idempotent','Verified source update, grant creation and library insertion commit together. Paid amount and line terms are immutable.','PaymentService.cs :: ConfirmOrder; Data/business-rules.sql'],
 ['Payments','Subscription cancellation','Cancel at period end','An ACTIVE cancelled subscription retains paid access until endsAt; PENDING cancellation grants no access.','CommerceController.cs :: CancelSubscription'],
 ['HTTP errors','400','Invalid request','Validation or FK/check/length constraint violations. ValidationProblemDetails may include field errors.','ApiSupport.cs; ApiController validation'],
 ['HTTP errors','401','Unauthenticated or invalid credentials/signature','Auth middleware may return an empty response body. Login/refresh/custom signature failures use Problem Details.','Program.cs; AuthController.cs; PaymentService.cs'],
 ['HTTP errors','403','Forbidden','Insufficient admin claim or paid/offline access. Authorization middleware may return an empty body.','Program.cs; AccessService'],
 ['HTTP errors','404','Resource not found','Owner-only lookups intentionally hide another user\'s resource.','Controllers/*.cs'],
 ['HTTP errors','409','Conflict','Duplicate/unique key, payment-state mismatch, session overlap, or concurrent transaction conflict.','ApiSupport.cs; PaymentService.cs; ReadingController.cs'],
 ['HTTP errors','429','Too many auth requests','Rate limiter rejects register/login/refresh bursts.','Program.cs'],
 ['HTTP errors','503','Integration unavailable','Content root or payment callback secret has not been configured.','DownloadsController.cs; PaymentService.cs'],
 ['HTTP errors','500','Unexpected server error','Safe generic message; server logs contain diagnostic details. Global failure, not a success response.','ApiSupport.cs :: TryHandleAsync'],
 ['Verification','Document basis','Implemented C# code and live OpenAPI','79 controller operations plus GET /health = 80. Response shapes were traced from C# return statements because generated OpenAPI mostly lacks IActionResult response schemas.','Controllers/*.cs; Program.cs; running /openapi/v1.json'],
 ['Verification','Existing test coverage','17 logic checks; HTTP smoke checks','PostgreSQL integration and cross-table trigger execution have not been run in this environment. JSON examples are illustrative.','README.md; BookNest.Verification; tools/http-smoke.ps1'],
];
const enumRows=[
 ['users.status',['ACTIVE','SUSPENDED','DELETED'],'Account lifecycle'],
 ['books.status',['DRAFT','PUBLISHED','ARCHIVED'],'Public catalog endpoints include PUBLISHED only'],
 ['books.fileFormat',['PDF','EPUB'],'Content MIME type and offline format'],
 ['reading_progress.status',['NOT_STARTED','READING','FINISHED'],'FINISHED sets finishedAt; reopening clears finishedAt'],
 ['downloads.status',['QUEUED','DOWNLOADING','COMPLETED','FAILED','REMOVED'],'Known file size must match COMPLETED bytesDownloaded'],
 ['reviews.status',['PUBLISHED','HIDDEN'],'Only PUBLISHED reviews contribute to aggregate ratings'],
 ['collections.visibility',['PRIVATE','SHARED','PUBLIC'],'Visibility does not share personal annotations'],
 ['orders.status',['PENDING','PAID','FAILED','CANCELLED','REFUNDED'],'Payment state is server-controlled'],
 ['order_items.accessType',['PURCHASE','RENTAL'],'Rental terms are captured at checkout'],
 ['book_entitlements.sourceType',['PURCHASE','RENTAL','PREMIUM'],'Access must also have valid source and time bounds'],
 ['user_subscriptions.status',['PENDING','ACTIVE','CANCELLED','EXPIRED'],'Cancelled paid periods may retain access'],
 ['subscription_plans.billingInterval',['MONTH','YEAR'],'Price/currency/cadence changes require a new plan'],
 ['collection_members.role',['VIEWER','EDITOR'],'Owner is stored as collections.userId'],
 ['collection_members.status',['INVITED','ACCEPTED','DECLINED','REVOKED'],'Only ACCEPTED grants shared-collection access'],
 ['highlights.colorCode',['YELLOW','GREEN','BLUE','PINK'],'Highlight palette'],
].flatMap(([field,values,meaning])=>values.map(value=>['Status codes',field,value,meaning,'Data/BookNestDbContext.cs']));
ruleRows.push(...enumRows);

const wb=Workbook.create();
const palette={navy:'#18354A',teal:'#167A75',light:'#EAF3F2',text:'#263846',muted:'#596C78',border:'#D9E3E7'};
const sheets={};
function column(n){let result='';for(n++;n;n=Math.floor((n-1)/26))result=String.fromCharCode(65+(n-1)%26)+result;return result;}
function base(name,cols,rows){
 const s=wb.worksheets.add(name);s.showGridLines=false;s.tabColor=palette.teal;
 s.getRangeByIndexes(0,0,rows,cols).format.font={name:'Arial',size:10,color:palette.text};
 s.getRangeByIndexes(0,0,rows,cols).format.verticalAlignment='center';
 s.getRange('A1:A'+rows).format.columnWidth=3;
 sheets[name]=s;return s;
}
function tableSheet(name,title,headers,rows,widths,options={}){
 const s=base(name,headers.length+1,rows.length+5);
 s.getRange('B2').values=[[title]];s.getRange('B2').format.font={name:'Arial',size:16,bold:true,color:palette.navy};
 s.getRangeByIndexes(2,1,1,headers.length).format.borders={bottom:{style:'thin',color:palette.teal}};
 s.getRangeByIndexes(3,1,1,headers.length).values=[headers];
 if(rows.length)s.getRangeByIndexes(4,1,rows.length,headers.length).values=rows;
 s.getRangeByIndexes(3,1,1,headers.length).format={fill:palette.navy,font:{name:'Arial',size:10,bold:true,color:'#FFFFFF'},horizontalAlignment:'center',verticalAlignment:'center',wrapText:true,rowHeight:34,borders:{insideVertical:{style:'thin',color:'#FFFFFF'}}};
 const body=s.getRangeByIndexes(4,1,rows.length,headers.length);body.format.wrapText=true;body.format.verticalAlignment='top';
 for(let i=0;i<widths.length;i++)s.getRangeByIndexes(3,i+1,rows.length+1,1).format.columnWidth=widths[i];
 const range='B4:'+column(headers.length)+String(rows.length+4);
 const table=s.tables.add(range,true,name.replace(/[^A-Za-z]/g,'')+'Table');table.showFilterButton=true;
 for(let i=0;i<rows.length;i++){
   const height=Math.max(options.minHeight??38,...rows[i].map((v,c)=>{
     const text=String(v??'');const usable=Math.max(6,widths[c]-3);return (text.split('\n').reduce((sum,line)=>sum+Math.max(1,Math.ceil(line.length/usable)),0))*14+12;
   }));
   s.getRangeByIndexes(i+4,1,1,headers.length).format.rowHeight=Math.min(380,height);
   if(i%2===1)s.getRangeByIndexes(i+4,1,1,headers.length).format.fill='#F2F6F7';
 }
 s.freezePanes.freezeRows(4);if(options.freezeColumns)s.freezePanes.freezeColumns(options.freezeColumns);
 return s;
}
const cover=base('Cover',9,30);cover.tabColor=palette.navy;
cover.mergeCells('B4:H5');cover.getRange('B4').values=[['BookNest']];cover.getRange('B4:H5').format.font={name:'Arial',size:28,bold:true,color:palette.navy};
cover.mergeCells('B7:H8');cover.getRange('B7').values=[['API Design & Reference']];cover.getRange('B7:H8').format.font={name:'Arial',size:22,bold:true,color:palette.teal};
cover.mergeCells('B10:H11');cover.getRange('B10').values=[['REST endpoints, request dictionary and response contracts']];cover.getRange('B10:H11').format.font={name:'Arial',size:12,color:palette.muted};
cover.mergeCells('B13:H13');cover.getRange('B13').values=[['Version 1.0  |  07 October 2026']];
cover.mergeCells('B15:H16');
cover.getRange('B15:H16').format.font={name:'Arial',size:12,bold:true,color:palette.navy};
cover.mergeCells('B18:H20');cover.getRange('B18').values=[['Authentication, catalog, reading, library, annotations, downloads, collections, commerce and administration']];cover.getRange('B18:H20').format.wrapText=true;
cover.mergeCells('B23:H25');cover.getRange('B23').values=[['Request and response definitions follow the current backend implementation. Payment provider integration and PostgreSQL end-to-end verification remain pending.']];cover.getRange('B23:H25').format.wrapText=true;cover.getRange('B23:H25').format.font={name:'Arial',size:10,color:palette.muted};
cover.getRange('B4:H25').format.columnWidth=12;cover.getRange('B4:H25').format.rowHeight=22;

tableSheet('API List','Implemented API operations',['API ID','Domain','HTTP method','Endpoint path','Access','Purpose','Request model','Success HTTP','Response model','Common errors','Business rules','Code source'],apiRows,[13,20,12,60,27,64,31,15,37,22,79,56],{freezeColumns:4,minHeight:44});
tableSheet('Request Fields','Request field dictionary',['API ID','Operation','Location','Field name','JSON type / model','Required','Default','Validation','Description','Example value'],requestRows,[13,65,16,28,32,19,23,58,64,45],{freezeColumns:2});
tableSheet('Response Fields','Response field dictionary',['Model','Field name','JSON type / model','Nullable','Description','Example value','Used by API IDs','Code source'],responseRows,[28,28,35,14,74,49,67,56],{freezeColumns:2});
tableSheet('Examples','Request and response examples',['API ID','Method','Concrete path','Request headers','Query parameters','JSON request body','Success HTTP','Response body example','Notes'],exampleRows,[13,12,58,47,46,105,16,118,67],{freezeColumns:2,minHeight:52});
tableSheet('Code Definitions','Codes, conventions and implementation rules',['Domain','Field / topic','Value / rule','Meaning and behavior','Code source'],ruleRows,[23,38,61,109,74],{freezeColumns:2,minHeight:44});
cover.getRange('B15').formulas=[[`=COUNTA('API List'!B5:B84)&" operations  |  English edition  |  Swift & Kotlin clients"`]];

// Counts and cross-references are checked independently before workbook export.
if(new Set(eps.map(e=>e.id)).size!==80)throw Error('API IDs not unique.');
if(requestRows.some(r=>!eps.some(e=>e.id===r[0])) || exampleRows.length!==eps.length)throw Error('Dictionary reference mismatch.');
for(const e of eps)if(e.body!=='None' && !requestRows.some(r=>r[0]===e.id && r[2]==='JSON body'))throw Error('Missing request fields '+e.id);
if(responseRows.some(r=>r[1]==='passwordHash' || r[1]==='contentStorageKey'))throw Error('Private field included in response dictionary.');
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'Cover!B15:H16',include:'values,formulas',tableMaxRows:2,tableMaxCols:7,maxChars:1500})).ndjson);
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#SPILL!',options:{useRegex:true,maxResults:30},maxChars:1500})).ndjson);
await fs.mkdir(path.join(work,'previews'),{recursive:true});
for(const [name,range] of [['Cover','A1:I27'],['API List','A1:G9'],['Request Fields','A1:K9'],['Response Fields','A1:I10'],['Examples','A1:J6'],['Code Definitions','A1:F9'],['API List','H1:M9']]) {
 const png=await wb.render({sheetName:name,range,scale:1,format:'png'});
 await fs.writeFile(path.join(work,'previews',name.replaceAll(' ','-')+(range.startsWith('H')?'-right':'')+'.png'),new Uint8Array(await png.arrayBuffer()));
}
const xlsx=await SpreadsheetFile.exportXlsx(wb);await xlsx.save(path.join(out,'BookNest_API_Design.xlsx'));
await fs.writeFile(path.join(work,'document-audit.json'),JSON.stringify({operations:eps.length,requestFields:requestRows.length,responseFields:responseRows.length,examples:exampleRows.length,rules:ruleRows.length,ids:eps.map(e=>({id:e.id,method:e.method,path:e.url,body:e.body,response:e.shape}))},null,2));
console.log(JSON.stringify({file:path.join(out,'BookNest_API_Design.xlsx'),operations:eps.length,requestFields:requestRows.length,responseFields:responseRows.length,examples:exampleRows.length,rules:ruleRows.length}));
