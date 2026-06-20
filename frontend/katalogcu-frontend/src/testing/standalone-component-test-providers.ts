import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentProviders, Provider } from '@angular/core';
import { provideRouter } from '@angular/router';

export function provideStandaloneComponentTestDeps(): Array<EnvironmentProviders | Provider> {
  return [
    provideRouter([]),
    provideHttpClient(),
    provideHttpClientTesting(),
  ];
}
