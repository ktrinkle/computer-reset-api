import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';

import { throwError, Observable } from 'rxjs';
import { retry, catchError } from 'rxjs/operators';
import { UserSmall, Timeslot, Signup, StateList, CityList, UserModel } from './data';


@Injectable({
  providedIn: 'root'
})
export class DataService {

  public eventIdPass: number;

  public userSmall: UserSmall;
  public userFull: UserModel;

  private REST_API_SERVER = "https://pcjrsidecar9525.azurewebsites.net";

  constructor(private httpClient: HttpClient) { }

  getServiceOptions(): any {

    const httpOptions = {
        headers: new HttpHeaders({
            'Content-Type': 'application/json; charset=utf-8',
            'Accept': 'application/json'
        })
    };

    return httpOptions;
  }

  handleError(error: HttpErrorResponse) {
    let errorMessage = 'Unknown error!';
    if (error.error instanceof ErrorEvent) {
      // Client-side errors
      errorMessage = `Error: ${error.error.message}`;
    } else {
      // Server-side errors
      errorMessage = `Error Code: ${error.status}\nMessage: ${error.message}`;
    }
    window.alert(errorMessage);
    return throwError(errorMessage);
  }
  
  /** GET - gets Azure Auth info from Facebook for UI to parse */
    public getAzureAuth() {
      var url = '/.auth/me';
      return this.httpClient.get(url);
    }
    
    public getAdmin(id: string){
      var url = this.REST_API_SERVER + '/api/computerreset/api/users/admin/' + encodeURIComponent(id) + '';
      return this.httpClient.get(url);
    }

    public getVolunteer(id: string){
      var url = this.REST_API_SERVER + '/api/computerreset/api/users/volunteer/' + encodeURIComponent(id) + '';
      return this.httpClient.get(url);
    }

    public getEvent(){
      var url = this.REST_API_SERVER + '/api/computerreset/api/events/show/open';
      return this.httpClient.get(url);
    }

      /** POST: calls after */
    public userInfo(user: UserSmall): any {
      var url = this.REST_API_SERVER + '/api/computerreset/api/users';
      return this.httpClient.post(url, user, this.getServiceOptions());
    } 

    public getState(): any {
      var url = this.REST_API_SERVER + '/api/computerreset/api/ref/state';
      return this.httpClient.get(url, this.getServiceOptions());
    }

    public getCity(id: number): any {
      let url = this.REST_API_SERVER + '/api/computerreset/api/ref/city/' + encodeURIComponent(id) + '';
      return this.httpClient.get(url, this.getServiceOptions());
    }

    public signupForEvent(eventReq: Signup): any {
      var url = this.REST_API_SERVER + '/api/computerreset/api/events/signup';
      return this.httpClient.post(url, eventReq, {headers : new HttpHeaders({ 'Content-Type': 'application/json' }),
        responseType: 'json',
        withCredentials: true,
      });


    }
}