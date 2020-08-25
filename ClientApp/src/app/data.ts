export interface ClaimPrincipal {
    access_token: string;
    expires_on: string;
    provider_name: string;
    user_claims?: (UserClaimsEntity)[] | null;
    user_id: string;
  }
  export interface UserClaimsEntity {
    typ: string;
    val: string;
  }

  export interface UserModel {
    id: number;
    firstName: string;
    lastName: string;
    cityName: string;
    stateCode: string;
    realName: string;
    facebookId: string;
    adminFlag: boolean;
    volunteerFlag: boolean;
  }

  export interface UserSmall {
    facebookId: string;
    firstName: string;
    lastName: string;
  }

  export interface Timeslot {
    Id: number;
    eventStartTms: Date;
    eventEndTms: Date;
    eventSlotCnt: number;
    eventOpenTms: Date;
    eventClosed: boolean;
    overbookCnt: number;
  }

  export interface Signup {
    fbId: string,
    eventId: number,
    cityNm: string,
    stateCd: string,
    realname: string,
    firstName: string;
    lastName: string;   
  }

  export interface StateList {
    id: number,
    stateCode: string,
    stateName: string
    usCities: string[]
  }

  export interface CityList {
    id: number,
    idState: number,
    city: string,
    county: string,
    latitude: number,
    longitude: number,
    idStateNavigation: string
  }

