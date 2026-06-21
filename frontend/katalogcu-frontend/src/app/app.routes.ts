import { Route, Routes } from '@angular/router';

// Public Components
import { HomeComponent } from './home/home.component';
import { ExploreComponent } from './explore/explore';
import { BlogComponent } from './blog/blog';
import { ServislerComponent } from './servisler/servisler';
import { PricesComponent } from './prices/prices';
import { LoginComponent } from './login/login';
import { RegisterComponent } from './register/register';
import { PublicViewComponent } from './public-view/public-view';
import { PublicCheckoutComponent } from './public-view/public-checkout/public-checkout';
import { PublicCatalogShowcaseComponent } from './public-catalog-showcase/public-catalog-showcase';
import { PublicCatalogViewerComponent } from './public-view/public-catalog-viewer/public-catalog-viewer';
import { PlatformLoginComponent } from './platform/platform-login/platform-login';
import { PlatformDashboardComponent } from './platform/platform-dashboard/platform-dashboard';
import { PlatformTenantDetailComponent } from './platform/platform-tenant-detail/platform-tenant-detail';

// Admin Components
import { AdminLayoutComponent } from './layout/admin-layout/admin-layout';
import { DashboardComponent } from './dashboard/dashboard';
import { CatalogDetailComponent } from './catalog-detail/catalog-detail';
import { CatalogsComponent } from './dashboard/catalogs/catalogs';
import { PartsComponent } from './dashboard/parts/parts';
import { CustomersComponent } from './dashboard/customers/customers';
import { SettingsComponent } from './dashboard/settings/settings';
import { CatalogAddComponent } from './dashboard/catalogs/catalog-add/catalog-add';
import { PartsAddComponent } from './dashboard/parts/parts-add/parts-add';
import { PartsImportComponent } from './dashboard/parts/parts-import/parts-import';
import { VisualFeedbackComponent } from './dashboard/visual-feedback/visual-feedback';
import { ChatQualityComponent } from './dashboard/chat-quality/chat-quality';
import { PolicyThresholdsComponent } from './dashboard/policy-thresholds/policy-thresholds';
import { OrdersComponent } from './dashboard/orders/orders';
import { planGuard } from './core/guards/plan.guard';
import { planSelectionGuard } from './core/guards/plan-selection.guard';
import { platformAdminGuard } from './core/guards/platform-admin.guard';
import { environment } from '../environments/environment';

const chatbotEnabled = environment.features.enableChatbot;
const ecommerceEnabled = environment.features.enableEcommerce;
const upgradePromptsEnabled = environment.features.enableUpgradePrompts;
const upgradeRoute: Route = upgradePromptsEnabled
  ? { path: 'upgrade', component: PricesComponent }
  : { path: 'upgrade', redirectTo: 'dashboard', pathMatch: 'full' };

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'public-view/:publicToken', component: PublicViewComponent },
  { path: 'p/:publicToken', component: PublicViewComponent },
  ...(ecommerceEnabled ? [{ path: 'public-view/:publicToken/checkout', component: PublicCheckoutComponent }] : []),
  ...(ecommerceEnabled ? [{ path: 'p/:publicToken/checkout', component: PublicCheckoutComponent }] : []),
  { path: 'view/:id', component: PublicCatalogShowcaseComponent },
  { path: 'view/:id/viewer/:pageIndex', component: PublicCatalogViewerComponent },
  { path: 'explore', component: ExploreComponent },
  { path: 'blog', component: BlogComponent },
  { path: 'services', component: ServislerComponent },
  { path: 'prices', component: PricesComponent },
  upgradeRoute,
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'platform/login', component: PlatformLoginComponent },
  { path: 'platform', component: PlatformDashboardComponent, canActivate: [platformAdminGuard] },
  { path: 'platform/tenants/:ownerId', component: PlatformTenantDetailComponent, canActivate: [platformAdminGuard] },
  {
    path: 'dashboard',
    component: AdminLayoutComponent,
    canActivate: [planSelectionGuard],
    children: [
      { path: '', component: DashboardComponent },
      { path: 'catalogs', component: CatalogsComponent },
      { path: 'catalog/:id', component: CatalogDetailComponent },
      { path: 'catalogs/new', component: CatalogAddComponent },
      { path: 'settings', component: SettingsComponent },
      ...(chatbotEnabled
        ? [
            { path: 'visual-feedback', component: VisualFeedbackComponent, canActivate: [planGuard], data: { minPlan: 2 } },
            { path: 'chat-quality', component: ChatQualityComponent, canActivate: [planGuard], data: { minPlan: 2 } },
            { path: 'policy-thresholds', component: PolicyThresholdsComponent, canActivate: [planGuard], data: { minPlan: 2 } },
            { path: 'ai', component: ChatQualityComponent, canActivate: [planGuard], data: { minPlan: 2 } }
          ]
        : []),
      ...(ecommerceEnabled
        ? [
            { path: 'customers', component: CustomersComponent, canActivate: [planGuard], data: { minPlan: 3 } },
            { path: 'parts', component: PartsComponent, canActivate: [planGuard], data: { minPlan: 3 } },
            { path: 'ecommerce', component: PartsComponent, canActivate: [planGuard], data: { minPlan: 3 } },
            { path: 'parts/new', component: PartsAddComponent },
            { path: 'parts/import', component: PartsImportComponent },
            { path: 'orders', component: OrdersComponent, canActivate: [planGuard], data: { minPlan: 3 } }
          ]
        : [])
    ]
  }
];
