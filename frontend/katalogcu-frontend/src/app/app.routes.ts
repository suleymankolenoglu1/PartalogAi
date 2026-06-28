import { Route, Routes } from '@angular/router';

// Public Components
import { HomeComponent } from './home/home.component';
import { PanelAccessComponent } from './panel-access/panel-access';
import { LoginComponent } from './login/login';
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
import { CatalogChatComponent } from './dashboard/catalog-chat/catalog-chat';
import { PolicyThresholdsComponent } from './dashboard/policy-thresholds/policy-thresholds';
import { OrdersComponent } from './dashboard/orders/orders';
import { planGuard } from './core/guards/plan.guard';
import { planSelectionGuard } from './core/guards/plan-selection.guard';
import { platformAdminGuard } from './core/guards/platform-admin.guard';
import { panelHomeGuard, panelHostGuard, portalHostGuard } from './core/guards/domain-host.guard';
import { environment } from '../environments/environment';

const chatbotEnabled = environment.features.enableChatbot;
const ecommerceEnabled = environment.features.enableEcommerce;
const upgradePromptsEnabled = environment.features.enableUpgradePrompts;
const upgradeRoute: Route = upgradePromptsEnabled
  ? { path: 'upgrade', component: PanelAccessComponent, canActivate: [panelHostGuard] }
  : { path: 'upgrade', redirectTo: 'dashboard', pathMatch: 'full' };

export const routes: Routes = [
  { path: '', component: HomeComponent, canActivate: [panelHomeGuard] },
  { path: 'public-view/:publicToken', component: PublicViewComponent, canActivate: [portalHostGuard] },
  { path: 'p/:publicToken', component: PublicViewComponent, canActivate: [portalHostGuard] },
  ...(ecommerceEnabled ? [{ path: 'public-view/:publicToken/checkout', component: PublicCheckoutComponent, canActivate: [portalHostGuard] }] : []),
  ...(ecommerceEnabled ? [{ path: 'p/:publicToken/checkout', component: PublicCheckoutComponent, canActivate: [portalHostGuard] }] : []),
  { path: 'view/:id', component: PublicCatalogShowcaseComponent, canActivate: [portalHostGuard] },
  { path: 'view/:id/viewer/:pageIndex', component: PublicCatalogViewerComponent, canActivate: [portalHostGuard] },
  { path: 'explore', redirectTo: '', pathMatch: 'full' },
  { path: 'blog', redirectTo: '', pathMatch: 'full' },
  { path: 'services', redirectTo: '', pathMatch: 'full' },
  { path: 'prices', redirectTo: 'login', pathMatch: 'full' },
  upgradeRoute,
  { path: 'login', component: LoginComponent, canActivate: [panelHostGuard] },
  { path: 'register', redirectTo: 'login', pathMatch: 'full' },
  { path: 'platform/login', component: PlatformLoginComponent, canActivate: [panelHostGuard] },
  { path: 'platform', component: PlatformDashboardComponent, canActivate: [panelHostGuard, platformAdminGuard] },
  { path: 'platform/tenants/:ownerId', component: PlatformTenantDetailComponent, canActivate: [panelHostGuard, platformAdminGuard] },
  {
    path: 'dashboard',
    component: AdminLayoutComponent,
    canActivate: [panelHostGuard, planSelectionGuard],
    children: [
      { path: '', component: DashboardComponent },
      { path: 'catalogs', component: CatalogsComponent },
      { path: 'catalog/:id', component: CatalogDetailComponent },
      { path: 'catalogs/new', component: CatalogAddComponent },
      { path: 'customers', component: CustomersComponent },
      { path: 'catalog-chat', component: CatalogChatComponent },
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
