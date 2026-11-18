import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  it('loads the starter dispatch board', async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent, NoopAnimationsModule],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/sites').flush([
      { id: 'IND-JSP', name: 'Jaspur Materials Recovery Site', timeZoneId: 'Asia/Kolkata' }
    ]);
    http.expectOne(request => request.url.endsWith('/movements')).flush([]);
    http.expectOne('/api/sites/IND-JSP/equipment').flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1').textContent).toContain('Dispatch board');
    http.verify();
  });
});
